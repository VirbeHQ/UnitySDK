using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Virbe.Core.Actions;
using Virbe.Core.Api;
using Virbe.Core.Logger;


namespace Virbe.Core
{
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
        private SocketCommunicationHandler _socketHandler;

        private VirbeActionPlayer _virbeActionPlayer;
        private Coroutine _autoBeingStateChangeCoroutine;


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

            _socketHandler = new SocketCommunicationHandler(ApiBeingConfig);
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
                _restPoolingHandler.EndCommunication();
                _virbeActionPlayer.StopCurrentAndScheduledActions();
                await _restPoolingHandler.Prepare(endUserId);
                SendNamedAction("conversation_start").Forget();
            }

            _restPoolingHandler.StartCommunicatoin();
        }

        public void StopConversation()
        {
            StopCurrentAndScheduledActions();
            _restPoolingHandler.EndCommunication();
        }

        public async UniTask RestoreConversation(string endUserId, string roomId)
        {
            await _restPoolingHandler.Prepare(endUserId, roomId);
            _restPoolingHandler.StartCommunicatoin();
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
                _socketHandler.StartSending().Forget();
            }
        }

        public void UserHasStoppedSpeaking()
        {
            if (CanChangeBeingState(Behaviour.InConversation))
            {
                ChangeBeingState(Behaviour.InConversation);
            }
            if (ApiBeingConfig.SttProtocol == SttConnectionProtocol.socket_io)
            {
                _socketHandler.StopSending();
            }
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
            if(ApiBeingConfig.SttProtocol == SttConnectionProtocol.socket_io)
            {
                _socketHandler.StopSending();
            }
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

        public async UniTaskVoid SendSpeechBytes(byte[] recordedAudioBytes)
        {
            if (recordedAudioBytes == null)
            {
                _logger.Log("Cannot send empty speech bytes");
                return;
            }

            if (ApiBeingConfig.SttProtocol == SttConnectionProtocol.socket_io)
            {
                _logger.LogError($"{SttConnectionProtocol.socket_io} protocol support only chunk audio sending at this moment.");
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
            _socketHandler.EnqueueChunk(recordedAudioBytes);
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