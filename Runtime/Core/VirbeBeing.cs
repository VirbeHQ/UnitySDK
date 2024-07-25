using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Virbe.Core.Actions;
using Virbe.Core.Api;
using Virbe.Core.Logger;
using Virbe.Core.Speech;


namespace Virbe.Core
{
    public class SocketHandler
    {

    }
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VirbeActionPlayer))]
    public class VirbeBeing : MonoBehaviour
    {
        public Action<BeingState> BeingStateChanged { get; set; }

        [Header("Being Configuration")]
        [Tooltip("E.g. \"Your API Config (check out Hub to get one or generate your Open Source)\"")]
        [SerializeField] protected internal TextAsset beingConfigJson;

        [SerializeField] protected internal bool autoStartConversation = true;
        [SerializeField] protected internal bool createNewEndUserInFocused = false;
        [SerializeField] private float focusedStateTimeout = 60f;
        [SerializeField] private float inConversationStateTimeout = 30f;
        [SerializeField] private float listeningStateTimeout = 10f;
        [SerializeField] private float requestErrorStateTimeout = 1f;

        [Header("Being Events")]
        [SerializeField] private UnityEvent<BeingState> onBeingStateChange = new BeingStateChangeEvent();
        [SerializeField] private UnityEvent<bool> onBeingMuteChange = new UnityEvent<bool>();
        [SerializeField] private UserActionEvent onUserAction = new UserActionEvent();
        [SerializeField] private BeingActionEvent onBeingAction = new BeingActionEvent();
        [SerializeField] private ConversationErrorEvent onConversationError = new ConversationErrorEvent();

        public Behaviour CurrentBeingBehaviour => _currentState._behaviour;
        public bool IsBeingSpeaking => _virbeActionPlayer.hasActionsToPlay();

        protected internal IApiBeingConfig ApiBeingConfig = null;

        private readonly BeingState _currentState = new BeingState();
        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(VirbeBeing));

        private string _overriddenSttLangCode = null;
        private string _overriddenTtsLanguage = null;

        private RestCommunicationHandler _restPoolingHandler;

        private VirbeActionPlayer _virbeActionPlayer;
        private Coroutine _autoBeingStateChangeCoroutine;

        private SocketIOClient.SocketIO _socketSttClient;
        private ConcurrentQueue<byte[]> _speechBytesAwaitingSend = new ConcurrentQueue<byte[]>();
        private StringBuilder _currentSttResult = new StringBuilder();

        private CancellationTokenSource _sttSocketTokenSource;
        private CancellationTokenSource _audioSocketSenderTokenSource;

        private void Awake()
        {
            _virbeActionPlayer = GetComponent<VirbeActionPlayer>();

            if (beingConfigJson != null)
            {
                ApiBeingConfig = VirbeUtils.ParseConfig(beingConfigJson.text);
            }
            else
            {
                ApiBeingConfig = new ApiBeingConfig();
            }
            _restPoolingHandler = new RestCommunicationHandler(ApiBeingConfig, 500);
            _restPoolingHandler.UserActionFired += (args) => onUserAction?.Invoke(args);
            _restPoolingHandler.BeingActionFired += (args) => _virbeActionPlayer.ScheduleNewAction(args);

            onBeingStateChange.AddListener((x) => BeingStateChanged?.Invoke(x));
        }

        private void Start()
        {
            ChangeBeingState(Behaviour.Idle);

            if (autoStartConversation)
            {
                StartNewConversation().Forget();
            }
        }

        private void OnDestroy()
        {
            _restPoolingHandler.Dispose();
        }

        public IApiBeingConfig ReadCurrentConfig()
        {
            if (ApiBeingConfig == null)
            {
                if (beingConfigJson != null)
                {
                    ApiBeingConfig = VirbeUtils.ParseConfig(beingConfigJson.text);
                }
            }

            return ApiBeingConfig;
        }

        public void InitializeFromConfigJson(string configJson)
        {
            beingConfigJson = new TextAsset(configJson);
            ApiBeingConfig = VirbeUtils.ParseConfig(configJson);
        }

        public void InitializeFromTextAsset(TextAsset textAsset)
        {
            beingConfigJson = textAsset;
            ApiBeingConfig = VirbeUtils.ParseConfig(textAsset.text);
        }

        public void StartNewConversationWithUserSession(string endUserId, string roomId)
        {
            RestoreConversation(endUserId, roomId).Forget();
        }

        public async UniTask StartNewConversation(bool forceNewEndUser = false, string endUserId = null)
        {
            if(ApiBeingConfig == null || !ApiBeingConfig.HasRoom)
            {
                _logger.LogError($"No api being config provided, can't start new coonversation");
                return;
            }

            if (!_restPoolingHandler.Initialized || forceNewEndUser)
            {
                _restPoolingHandler.StopPolling();
                _virbeActionPlayer.StopCurrentAndScheduledActions();
                await _restPoolingHandler.Prepare(endUserId);
                SendNamedAction("conversation_start").Forget();
            }

            _restPoolingHandler.StartPooling();
        }

        public void StopConversation()
        {
            StopCurrentAndScheduledActions();
            _restPoolingHandler.StopPolling();
        }

        public async UniTask RestoreConversation(string endUserId, string roomId)
        {
            await _restPoolingHandler.Prepare(endUserId, roomId);
            _restPoolingHandler.StartPooling();
        }

        private async UniTaskVoid SocketAudioSendLoop(CancellationToken cancelationToken)
        {
            while (!cancelationToken.IsCancellationRequested)
            {
                if(_socketSttClient.Connected && _speechBytesAwaitingSend.TryDequeue(out var chunk))
                {
                    await _socketSttClient.EmitAsync("audio", AudioConverter.FromBytesToBase64(chunk));
                }
                if (cancelationToken.IsCancellationRequested)
                {
                    return;
                }
                #if UNITY_2023_1_OR_NEWER
                    await UniTask.WaitForEndOfFrame();
                #else
                   await UniTask.WaitForEndOfFrame(this); // this is MonoBehaviour
                #endif
            }
        }

        #region Triggers

        public void UserHasApproached()
        {
            if (CanChangeBeingState(Behaviour.Focused))
            {
                if (createNewEndUserInFocused && _currentState._behaviour == Behaviour.Idle)
                {
                    StartNewConversation(createNewEndUserInFocused).Forget();
                }

                ChangeBeingState(Behaviour.Focused);
            }
        }

        public void UserHasEngagedInConversation()
        {
            if (CanChangeBeingState(Behaviour.InConversation))
            {
                ChangeBeingState(Behaviour.InConversation);
            }
        }

        public void UserHasStartedSpeaking()
        {
            if (CanChangeBeingState(Behaviour.Listening))
            {
                ChangeBeingState(Behaviour.Listening);
            }

            if (ApiBeingConfig.SttProtocol == SttConnectionProtocol.socket_io)
            {
                ConnectToSttSocket().Forget();
                _audioSocketSenderTokenSource?.Cancel();
                _audioSocketSenderTokenSource = new CancellationTokenSource();
                SocketAudioSendLoop(_audioSocketSenderTokenSource.Token).Forget();
            }
        }

        public void UserHasStoppedSpeaking()
        {
            if (CanChangeBeingState(Behaviour.InConversation))
            {
                ChangeBeingState(Behaviour.InConversation);
            }
            _audioSocketSenderTokenSource?.Cancel();
            DisposeSocketConnection();
        }

        public void UserHasDisengagedFromConversation()
        {
            if (CanChangeBeingState(Behaviour.Focused))
            {
                ChangeBeingState(Behaviour.Focused);
            }
        }

        public void UserHasLeft()
        {
            if (CanChangeBeingState(Behaviour.Idle))
            {
                ChangeBeingState(Behaviour.Idle);
            }
            DisposeSocketConnection();
        }
        #endregion

        public void SetBeingMute(bool isMuted)
        {
            _currentState.isMuted = isMuted;
            if (_virbeActionPlayer)
            {
                _virbeActionPlayer.MuteAudio(isMuted);
            }
            onBeingMuteChange.Invoke(_currentState.isMuted);
        }


        public void SetOverrideDefaultSttLangCode(string sttLangCode)
        {
            _overriddenSttLangCode = sttLangCode;
        }

        public void SetOverrideDefaultTtsLanguage(string ttsLanguage)
        {
            _overriddenTtsLanguage = ttsLanguage;
        }

        //private void ConsumeApiException(Exception error)
        //{
        //    ChangeBeingState(Behaviour.RequestError);

        //    _logger.LogError(error.Message);
        //    onConversationError?.Invoke(error);
        //}

        public async UniTaskVoid SendSpeechBytes(byte[] recordedAudioBytes)
        {
            if (recordedAudioBytes == null)
            {
                _logger.Log("[VIRBE] Cannot send empty speech bytes");
                return;
            }

            if (ApiBeingConfig.SttProtocol == SttConnectionProtocol.socket_io)
            {
                _logger.LogError($"[VIRBE] {SttConnectionProtocol.socket_io} protocol support only chunk audio sending at this moment.");
                return;
            }

            if (ApiBeingConfig.RoomEnabled)
            {
                var sendTask = _restPoolingHandler.SendSpeech(recordedAudioBytes);
                await sendTask;

                if (sendTask.IsFaulted)
                {
                    _logger.Log("Failed to send speech: " + sendTask.Exception?.Message);
                }
                else if (sendTask.IsCompleted)
                {
                    _logger.Log("Sent speech");
                }
            }
        }

        public void SendSpeechChunk(byte[] recordedAudioBytes)
        {
            if (recordedAudioBytes == null || recordedAudioBytes.Length == 0)
            {
                _logger.Log("[VIRBE] Audio chunk is empty - ommitting");
                return;
            }

            if (ApiBeingConfig.SttProtocol != SttConnectionProtocol.socket_io)
            {
                _logger.LogError($"[VIRBE] Only {SttConnectionProtocol.socket_io} protocol support chunk audio sending at this moment.");
                return;
            }
            if (ApiBeingConfig.RoomEnabled)
            {
                if(_socketSttClient == null)
                {
                    _logger.LogError($"[VIRBE] Socket not created, could not send speech chunk");
                    return;
                }
                _speechBytesAwaitingSend.Enqueue(recordedAudioBytes);
            }
        }

        public async UniTask SendNamedAction(string name, string value = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                _logger.Log("Cannot send empty NamedAction");
                return;
            }

            if (ApiBeingConfig.RoomEnabled)
            {
                var sendTask = _restPoolingHandler.SendNamedAction(name, value);
                await sendTask;

                if (sendTask.IsFaulted)
                {
                    _logger.Log("Failed to send namedAction: " + sendTask.Exception?.Message);
                }
                else if (sendTask.IsCompleted)
                {
                    _logger.Log("Sent namedAction");
                }
            }
        }

        public void SubmitInput(string submitPayload, string storeKey, string storeValue)
        {
            // TODO send inputs to room api
        }

        public async UniTask SendText(string capturedUtterance)
        {
            if (ApiBeingConfig.RoomEnabled)
            {
                var sendTask = _restPoolingHandler.SendText(capturedUtterance);
                await sendTask;

                if (sendTask.IsFaulted)
                {
                    _logger.Log("Failed to send text: " + sendTask.Exception?.Message);
                }
                else if (sendTask.IsCompleted)
                {
                    _logger.Log("Sent text");
                }
            }
        }

        public void StopCurrentAndScheduledActions()
        {
            _virbeActionPlayer.StopCurrentAndScheduledActions();
        }

        private bool CanChangeBeingState(Behaviour newBehaviour)
        {
            Behaviour[] allowedCurrentStates;
            switch (newBehaviour)
            {
                case Behaviour.Idle:
                    allowedCurrentStates = new[] { Behaviour.Focused };
                    break;
                case Behaviour.Focused:
                    allowedCurrentStates = new[] { Behaviour.Idle, Behaviour.InConversation };
                    break;
                case Behaviour.InConversation:
                    allowedCurrentStates = new[]
                    {
                        Behaviour.Focused, Behaviour.Idle, Behaviour.InConversation, Behaviour.Listening,
                        Behaviour.PlayingBeingAction, Behaviour.RequestError
                    };
                    break;
                case Behaviour.Listening:
                    allowedCurrentStates = new[] { Behaviour.Focused, Behaviour.Idle, Behaviour.InConversation };
                    break;
                case Behaviour.RequestProcessing:
                    allowedCurrentStates = new[]
                    {
                        Behaviour.Focused, Behaviour.Idle, Behaviour.InConversation, Behaviour.Listening,
                        Behaviour.RequestError
                    };
                    break;
                case Behaviour.RequestReceived:
                    allowedCurrentStates = new[] { Behaviour.RequestProcessing };
                    break;
                case Behaviour.RequestError:
                    allowedCurrentStates = new[] { Behaviour.RequestProcessing };
                    break;
                case Behaviour.PlayingBeingAction:
                    allowedCurrentStates = new[] { Behaviour.RequestReceived };
                    break;
                default:
                    return false;
            }

            return allowedCurrentStates.Contains(_currentState._behaviour);
        }

        private void DisposeSocketConnection()
        {
            if (ApiBeingConfig.SttProtocol == SttConnectionProtocol.socket_io)
            {
                if (_socketSttClient?.Connected == true)
                {
                    DisconnectFromSttSocket().Forget();
                }
                else
                {
                    _speechBytesAwaitingSend.Clear();
                    _sttSocketTokenSource.Cancel();
                }
            }
        }

        private async UniTaskVoid ConnectToSttSocket()
        {
            _sttSocketTokenSource?.Cancel();
            _sttSocketTokenSource = new CancellationTokenSource();
            _socketSttClient = new SocketIOClient.SocketIO(ApiBeingConfig.BaseUrl);
            _socketSttClient.Options.EIO = SocketIO.Core.EngineIO.V4;
            _socketSttClient.Options.Path = ApiBeingConfig.SttPath;
            _currentSttResult.Clear();

            _logger.Log($"Try connecting to socket.io endpoint");

            _socketSttClient.On("upgrade", (response) =>
            {
                _logger.Log($"Upgraded transport: ${response}");
            });

            _socketSttClient.On("recognizing", (response) =>
            {
                _currentSttResult.Append(response);
                _logger.Log($"Recognized text: {response}");
            });

            _socketSttClient.On("connect_error", (response) =>
            {
                _logger.LogError($"Connection error : {response}");
            });

            _socketSttClient.OnConnected += (sender, args) =>
            {
                _logger.Log($"Connected to the stt socket .");
            };

            _socketSttClient.OnError += (sender, args) =>
            {
                _logger.Log($"Socket error: {args}");
            };

            _socketSttClient.OnDisconnected += (sender, args) =>
            {
                _sttSocketTokenSource?.Cancel();
                if(_currentSttResult.Length > 0)
                {
                    SendText(_currentSttResult.ToString()).Forget();
                    _currentSttResult.Clear();
                }
                _logger.Log($"Disconnected from the stt socket {args}");
            };

            await _socketSttClient.ConnectAsync(_sttSocketTokenSource.Token);
        }

        private async UniTaskVoid DisconnectFromSttSocket()
        {
            _sttSocketTokenSource?.Cancel();
            var tempSocketHandle = _socketSttClient;
            await Task.Delay(1000);
            SendText(_currentSttResult.ToString()).Forget();
            _currentSttResult.Clear();
            await tempSocketHandle.DisconnectAsync();
            tempSocketHandle.Dispose();
            tempSocketHandle = null;
        }

        private void ChangeBeingState(Behaviour newBehaviour)
        {
            if (_currentState._behaviour != newBehaviour)
            {
                _logger.Log($"Changing from state: {_currentState._behaviour} to {newBehaviour}");

                _currentState._behaviour = newBehaviour;
                onBeingStateChange?.Invoke(_currentState);
            }

            ScheduleChangeBeingStateAfterTimeoutIfNeeded();
        }

        private void ScheduleChangeBeingStateAfterTimeoutIfNeeded()
        {
            if (_autoBeingStateChangeCoroutine != null)
            {
                // Canceling previous timeout to avoid unexpected changes
                StopCoroutine(_autoBeingStateChangeCoroutine);
            }

            switch (_currentState._behaviour)
            {
                case Behaviour.Listening:
                    // make sure being is not constantly waiting for recording
                    // Next state: InConversation
                    _autoBeingStateChangeCoroutine =
                        StartCoroutine(WaitAndChangeToState(listeningStateTimeout, Behaviour.InConversation));
                    break;
                case Behaviour.InConversation:
                    // make sure being is not constantly waiting for recording
                    // Next state: InConversation
                    _autoBeingStateChangeCoroutine =
                        StartCoroutine(WaitAndChangeToState(inConversationStateTimeout, Behaviour.Focused));
                    break;
                case Behaviour.RequestError:
                    _autoBeingStateChangeCoroutine =
                        StartCoroutine(WaitAndChangeToState(requestErrorStateTimeout, Behaviour.InConversation));
                    break;
                case Behaviour.Focused:
                    // make sure being is not constantly focused on user after conversation
                    // Next state: Idle
                    _autoBeingStateChangeCoroutine =
                        StartCoroutine(WaitAndChangeToState(focusedStateTimeout, Behaviour.Idle));
                    break;
            }
        }

        private IEnumerator WaitAndChangeToState(float timeout, Behaviour newBehaviour)
        {
            yield return new WaitForSeconds(timeout);
            ChangeBeingState(newBehaviour);
        }

        public void OnUserAction(UserAction userAction)
        {
            _currentState.lastUserAction = userAction;
            onUserAction?.Invoke(userAction);
        }

        public void OnBeingActionStarted(BeingAction beingAction)
        {
            ChangeBeingState(Behaviour.PlayingBeingAction);

            _currentState.lastBeingAction = beingAction;
            onBeingAction?.Invoke(beingAction);
        }

        public void OnBeingActionEnded(BeingAction beingAction)
        {
            ChangeBeingState(Behaviour.InConversation);
        }
    }
}