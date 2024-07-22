using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Plugins.Virbe.Core.Api;
using UnityEngine;
using UnityEngine.Events;
using Virbe.Core.Actions;
using Virbe.Core.Api;
using Virbe.Core.Logger;


namespace Virbe.Core
{
    [Serializable]
    public class BeingStateChangeEvent : UnityEvent<BeingState>
    {
    }

    [Serializable]
    public class UserActionEvent : UnityEvent<UserAction>
    {
    }


    [Serializable]
    public class BeingActionEvent : UnityEvent<BeingAction>
    {
    }

    [Serializable]
    public class ConversationErrorEvent : UnityEvent<Exception>
    {
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(VirbeActionPlayer))]
    public class VirbeBeing : MonoBehaviour
    {
        private const string ConfigurationType = nameof(VirbeBeing);
        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(VirbeBeing));

        [Header("Being Configuration")]
        [SerializeField]
        [Tooltip("E.g. \"Your API Config (check out Hub to get one or generate your Open Source)\"")]
        protected internal TextAsset beingConfigJson;

        [SerializeField] protected internal bool autoStartConversation = true;
        [SerializeField] protected internal bool createNewEndUserInFocused = false;
        [SerializeField] private float focusedStateTimeout = 60f;
        [SerializeField] private float inConversationStateTimeout = 30f;
        [SerializeField] private float listeningStateTimeout = 10f;
        [SerializeField] private float requestErrorStateTimeout = 1f;

        [Header("Being Events")] [SerializeField]
        public UnityEvent<BeingState> onBeingStateChange = new BeingStateChangeEvent();

        public UnityEvent<bool> onBeingMuteChange = new UnityEvent<bool>();
        [SerializeField] public UserActionEvent onUserAction = new UserActionEvent();
        [SerializeField] public BeingActionEvent onBeingAction = new BeingActionEvent();

        [SerializeField] public ConversationErrorEvent onConversationError = new ConversationErrorEvent();

        private readonly BeingState _currentState = new BeingState();

        private string _overriddenSttLangCode = null;
        private string _overriddenTtsLanguage = null;

        private RoomApiService _roomApiService;
        private VirbeActionPlayer _virbeActionPlayer;

        private VirbeUserSession _currentUserSession;

        protected internal IApiBeingConfig ApiBeingConfig = null;

        private Coroutine _autoBeingStateChangeCoroutine;
        private CancellationTokenSource pollingMessagesCancelletion;
        private Task pollingMessageTask;
        private readonly int _poolingInterval = 500;

        public IApiBeingConfig ReadCurrentConfig()
        {
            if (ApiBeingConfig == null)
            {
                if (beingConfigJson != null)
                {
                    ApiBeingConfig = JsonUtility.FromJson<ApiBeingConfig>(beingConfigJson.text);
                }
            }

            return ApiBeingConfig;
        }

        internal IApiBeingConfig VirbeApiBeing()
        {
            if (ApiBeingConfig == null)
            {
                if (beingConfigJson != null)
                {
                    ApiBeingConfig = JsonUtility.FromJson<ApiBeingConfig>(beingConfigJson.text);
                }
                else
                {
                    ApiBeingConfig = new ApiBeingConfig();
                }
            }

            if (!ApiBeingConfig.HasValidHostDomain())
            {
                throw new Exception(
                    $"Host Domain defined in {ConfigurationType} is empty, but should be defined.");
            }

            if (!ApiBeingConfig.HasValidApiAccessKey())
            {
                throw new Exception(
                    $"Being Room Api Access Key defined in {ConfigurationType} is empty, but should be defined.");
            }

            return ApiBeingConfig;
        }

        public void InitializeFromConfigJson(string configJson)
        {
            beingConfigJson = new TextAsset(configJson);
            ApiBeingConfig = JsonUtility.FromJson<ApiBeingConfig>(configJson);
        }

        public void InitializeFromConfig(ApiBeingConfig config)
        {
            beingConfigJson = new TextAsset(JsonUtility.ToJson(config));
            ApiBeingConfig = config;
        }

        public void InitializeFromTextAsset(TextAsset textAsset)
        {
            beingConfigJson = textAsset;
            ApiBeingConfig = JsonUtility.FromJson<ApiBeingConfig>(textAsset.text);

        }

        private void Awake()
        {
            _virbeActionPlayer = GetComponent<VirbeActionPlayer>();

            if (beingConfigJson != null)
            {
                ApiBeingConfig = JsonConvert.DeserializeObject<ApiBeingConfig>(beingConfigJson.text);
            }
            else
            {
                ApiBeingConfig = new ApiBeingConfig();
            }
        }

        private void Start()
        {
            ChangeBeingState(Behaviour.Idle);

            if (autoStartConversation)
            {
                StartNewConversation();
            }
        }

        private void OnDestroy()
        {
            StopRoomMessagePollingIfNeeded();
        }

        public void StartNewConversationWithUserSession(string endUserId = null, string roomId = null)
        {
            RestoreUserSession(endUserId, roomId);

            StartNewConversation();
        }

        private void RestoreUserSession(string endUserId, string conversationId)
        {
            _currentUserSession = new VirbeUserSession(endUserId, conversationId);
        }

        public void StartNewConversation(bool forceNewEndUser = false)
        {
            if(ApiBeingConfig == null || ApiBeingConfig.HasRoom)
            {
                Debug.LogError($"No api being config provided, can't start new coonversation");
                return;
            }
            if (_currentUserSession == null || forceNewEndUser)
            {
                StopRoomSession();

                _currentUserSession = new VirbeUserSession();
            }

            if (ApiBeingConfig.RoomEnabled)
            {
                StopRoomMessagePollingIfNeeded();

                if (_currentUserSession != null)
                {
                    if (!_currentUserSession.HasRoomId())
                    {
                        StartRoomSession();
                    }
                    else
                    {
                        StartRoomMessagePolling();
                    }
                }
                else
                {
                    Debug.LogError("No user session available to start room session");
                }
            }
        }

        public VirbeUserSession GetUserSession()
        {
            return _currentUserSession;
        }

        public void StopRoomSession()
        {
            StopCurrentAndScheduledActions();
            StopRoomMessagePollingIfNeeded();
        }

        private async void StartRoomSession()
        {
            _roomApiService = ApiBeingConfig.CreateRoom(_currentUserSession.EndUserId);

            var createRoomTask = _roomApiService.CreateRoom();

            await createRoomTask;

            if (createRoomTask.IsFaulted)
            {
                // Handle any errors that occurred during room creation
                Debug.LogError("Failed to create room: " + createRoomTask.Exception?.Message);

                // TODO display UI error message that his config is wrong
            }
            else if (createRoomTask.IsCompleted)
            {
                // Get the created room from the task result
                var createdRoom = createRoomTask.Result;
                Debug.Log("Room created successfully. Room ID: " + createdRoom.id);

                _currentUserSession.UpdateSession(_currentUserSession.EndUserId, createdRoom.id);

                SendNamedAction("conversation_start");
                StartRoomMessagePolling();
            }
        }

        private async void StartRoomMessagePolling()
        {
            await MessagePollingTask();
        }

        private async Task MessagePollingTask()
        {
            pollingMessagesCancelletion = new CancellationTokenSource();

            while (!pollingMessagesCancelletion.IsCancellationRequested)
            {
                await Task.Delay(_poolingInterval);

                try
                {
                    // TODO Debug.Log might generate errors in Mobile platforms, remove it, or use a wrapper like UniTask (which is additional dependency so is it worth it?)
                    var getMessagesTask = _roomApiService.PollNewMessages();
                    await getMessagesTask;

                    if (getMessagesTask.IsFaulted)
                    {
                        // Handle any errors that occurred during room creation
                        Debug.LogError("Failed to get messages: " + getMessagesTask.Exception?.Message);
                        continue;
                    }

                    if (!getMessagesTask.IsCompleted)
                    {
                        // Handle any errors that occurred during room creation
                        Debug.LogError("Taks has been awaited but is not completed");
                        continue;
                    }

                    // Get the messages from the task result
                    var messages = getMessagesTask.Result;

                    if (messages == null || messages.count == 0)
                    {
                        continue;
                    }

                    Debug.Log($"Got new messages: {messages?.count}");
                    messages.results.Reverse();
                    foreach (var message in messages.results)
                    {
                        Debug.Log("Got message: " + message?.action?.text?.text);
                        if (message.participantType == "EndUser")
                        {
                            // TODO handle UserAction message on MainThread
                            OnUserAction(new UserAction(message.action?.text?.text));
                        }
                        else if (message.participantType == "Api" || message.participantType == "User")
                        {
                            try
                            {
                                var getVoiceTask = _roomApiService.GetRoomMessageVoiceData(message);
                                await getVoiceTask;

                                if (getVoiceTask.IsFaulted)
                                {
                                    // TODO should we try to get the voice data again?
                                    // Handle any errors that occurred during room creation
                                    Debug.LogError("Failed to get voice data: " +
                                                   getVoiceTask.Exception?.Message);
                                }
                                else if (getVoiceTask.IsCompleted)
                                {
                                    var action = new BeingAction
                                    {
                                        text = message?.action?.text.text,
                                        speech = getVoiceTask.Result.data,
                                        marks = getVoiceTask.Result.marks,
                                        cards = message?.action?.uiAction?.value?.cards,
                                        buttons = message?.action?.uiAction?.value?.buttons,
                                    };
                                    _virbeActionPlayer.ScheduleNewAction(action);
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.LogError("Failed to get voice data: " + e.Message);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("Failed to get messages: " + e.Message);
                }
            }
        }

        private void StopRoomMessagePollingIfNeeded()
        {
            if (pollingMessagesCancelletion != null)
            {
                pollingMessagesCancelletion.Cancel();
            }
        }

        #region Triggers

        public void UserHasApproached()
        {
            if (CanChangeBeingState(Behaviour.Focused))
            {
                if (createNewEndUserInFocused && _currentState._behaviour == Behaviour.Idle)
                {
                    StartNewConversation(createNewEndUserInFocused);
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
        }

        public void UserHasStoppedSpeaking()
        {
            if (CanChangeBeingState(Behaviour.InConversation))
            {
                ChangeBeingState(Behaviour.InConversation);
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
        }

        #endregion

        public void MuteBeing()
        {
            _currentState.isMuted = true;
            if (_virbeActionPlayer) _virbeActionPlayer.MuteAudio(true);
            onBeingMuteChange.Invoke(_currentState.isMuted);
        }

        public void UnmuteBeing()
        {
            _currentState.isMuted = false;
            if (_virbeActionPlayer) _virbeActionPlayer.MuteAudio(false);
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


        private void ConsumeApiException(Exception error)
        {
            ChangeBeingState(Behaviour.RequestError);

            _logger.LogError(error.Message);
            onConversationError?.Invoke(error);
        }

        public async void SendSpeechBytes(byte[] recordedAudioBytes)
        {
            if (recordedAudioBytes == null)
            {
                Debug.Log("Cannot send empty speech bytes");
                return;
            }

            // TODO send only if room session is active
            if (ApiBeingConfig.RoomEnabled)
            {
                var sendTask = _roomApiService.SendSpeech(recordedAudioBytes);
                await sendTask;

                if (sendTask.IsFaulted)
                {
                    Debug.Log("Failed to send speech: " + sendTask.Exception?.Message);
                }
                else if (sendTask.IsCompleted)
                {
                    Debug.Log("Sent speech: " + sendTask.Result?.id);
                }
            }
        }

        public async void SendNamedAction(string name, string value = null)
        {
            if (name == null)
            {
                Debug.Log("Cannot send empty NamedAction");
                return;
            }

            // TODO send only if room session is active
            if (ApiBeingConfig.RoomEnabled)
            {
                var sendTask = _roomApiService.SendNamedAction(name, value);
                await sendTask;

                if (sendTask.IsFaulted)
                {
                    Debug.Log("Failed to send namedAction: " + sendTask.Exception?.Message);
                }
                else if (sendTask.IsCompleted)
                {
                    Debug.Log("Sent namedAction: " + sendTask.Result?.id);
                }
            }
        }

        public async void SubmitInput(string submitPayload, string storeKey, string storeValue)
        {
            // TODO send inputs to room api
        }

        public async void SendText(string capturedUtterance)
        {
            //TODO send only if room session is active

            if (ApiBeingConfig.RoomEnabled)
            {
                var sendTask = _roomApiService.SendText(capturedUtterance);
                await sendTask;

                if (sendTask.IsFaulted)
                {
                    Debug.Log("Failed to send text: " + sendTask.Exception?.Message);
                }
                else if (sendTask.IsCompleted)
                {
                    Debug.Log("Sent text: " + sendTask.Result?.id);
                }
            }
        }

        public Behaviour getCurrentBeingBehaviour()
        {
            return _currentState._behaviour;
        }

        public bool isBeingSpeaking()
        {
            return _virbeActionPlayer.hasActionsToPlay();
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

            yield return null;
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