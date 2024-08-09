using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using Virbe.Core.Actions;
using Virbe.Core.Logger;
using Virbe.Core.ThirdParty.SavWav;
using Virbe.Core.VAD;

namespace Virbe.Core
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VirbeActionPlayer))]
    public class VirbeBeing : MonoBehaviour
    {
        public event Action<BeingState> OnBeingStateChanged;
        public event Action<UserAction> OnUserAction;
        public event Action<BeingAction> OnBeingAction;
        public event Action<bool> OnBeingMuteChange;

        public event Action<string> UserSpeechRecognized;

        public Behaviour CurrentBeingBehaviour => _currentState._behaviour;
        public bool IsBeingSpeaking => _virbeActionPlayer.HasActionsToPlay;
        public TextAsset ActiveConfigAsset => beingConfigJson;
        public IApiBeingConfig ApiBeingConfig { get; private set; }

        internal event Action ConversationStarted;
        internal event Action ConversationEnded;

        internal event Action UserStartSpeaking;
        internal event Action UserStopSpeaking;
        internal event Action UserLeftConversation;

        [Header("Being Configuration")]
        [Tooltip("E.g. \"Your API Config (check out Hub to get one or generate your Open Source)\"")]
        [SerializeField] protected internal TextAsset beingConfigJson;

        [SerializeField] protected internal bool autoStartConversation = true;
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

        private readonly BeingState _currentState = new BeingState();
        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(VirbeBeing));

        private string _overriddenSttLangCode = null;
        private string _overriddenTtsLanguage = null;

        private CommunicationSystem _communicationSystem;

        private VirbeActionPlayer _virbeActionPlayer;
        private Coroutine _autoBeingStateChangeCoroutine;
        private bool _saveWaveSamplesDebug = false;

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
            _communicationSystem = new CommunicationSystem(this);
            _communicationSystem.UserActionFired += CallUserAction;
            _communicationSystem.BeingActionFired += (args) => _virbeActionPlayer.ScheduleNewAction(args);
            _communicationSystem.UserSpeechRecognized += (speech) => UserSpeechRecognized?.Invoke(speech);
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
            _communicationSystem.Dispose();
            StopAllCoroutines();
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

        public void SetSettings(bool autoStartConversation = true, float focusedStateTimeout= 60, float inConversationTimeout = 30, float listeningTimeout = 10)
        {
            this.autoStartConversation = autoStartConversation;
            this.focusedStateTimeout = focusedStateTimeout;
            this.inConversationStateTimeout = inConversationTimeout;
            this.listeningStateTimeout = listeningTimeout;
        }

        public void StartNewConversationWithUserSession(string endUserId, string roomId) => RestoreConversation(endUserId, roomId).Forget();

        public async UniTask StartNewConversation(bool forceNewEndUser = false, string endUserId = null)
        {
            if(ApiBeingConfig == null)
            {
                _logger.LogError($"No api being config provided, can't start new coonversation");
                return;
            }

            if (!_communicationSystem.Initialized || forceNewEndUser)
            {
                _virbeActionPlayer.StopCurrentAndScheduledActions();
                await _communicationSystem.InitializeWith(endUserId);
                SendNamedAction("conversation_start");
            }
            ConversationStarted?.Invoke();
        }

        public void StopConversation()
        {
            StopCurrentAndScheduledActions();
            ConversationEnded?.Invoke();
        }

        public void UserHasApproached(bool createNewEndUser = false)
        {
            if (CanChangeBeingState(Behaviour.Focused))
            {
                if (createNewEndUser && _currentState._behaviour == Behaviour.Idle)
                {
                    StartNewConversation(createNewEndUser).Forget();
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
            UserStartSpeaking?.Invoke();
        }

        public void UserHasStoppedSpeaking()
        {
            if (CanChangeBeingState(Behaviour.InConversation))
            {
                ChangeBeingState(Behaviour.InConversation);
            }
            UserStopSpeaking?.Invoke();
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
            UserLeftConversation?.Invoke();
        }

        public void SetBeingMute(bool isMuted)
        {
            _currentState.isMuted = isMuted;
            if (_virbeActionPlayer)
            {
                _virbeActionPlayer.MuteAudio(isMuted);
            }
            onBeingMuteChange.Invoke(_currentState.isMuted);
            OnBeingMuteChange?.Invoke(_currentState.isMuted);
        }

        public void SetOverrideDefaultSttLangCode(string sttLangCode)
        {
            _overriddenSttLangCode = sttLangCode;
        }

        public void SetOverrideDefaultTtsLanguage(string ttsLanguage)
        {
            _overriddenTtsLanguage = ttsLanguage;
        }

        public void SendSpeechBytes(float[] recordedAudio, bool streamed)
        {
            if (recordedAudio == null)
            {
                _logger.Log("Cannot send empty speech bytes");
                return;
            }

            var recordingBytes = SavWav.GetWavF(
                  samples: recordedAudio,
                  frequency: (uint)Mic.Instance.Frequency,
                  channels: (ushort)Mic.Instance.Channels,
                  length: out _
              );

            if (_saveWaveSamplesDebug)
            {
                var path = Path.Combine(Application.dataPath, "TestRecordings");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                File.WriteAllBytes($"{Application.dataPath}/TestRecordings/{DateTime.Now:HH_mm_ss}.wav",
                    recordingBytes);
            }
            _communicationSystem.SendAudio(recordingBytes, streamed).Forget();
        }

        public void SendNamedAction(string name, string value = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                _logger.Log("Cannot send empty NamedAction");
                return;
            }

            if (ApiBeingConfig.HasRoom)
            {
                _communicationSystem.SendNamedAction(name, value).Forget();
            }
        }

        public void SubmitInput(string submitPayload, string storeKey, string storeValue)
        {
            // TODO send inputs to room api
        }

        public void SendText(string capturedUtterance)
        {
            if (ApiBeingConfig.HasRoom)
            {
                _communicationSystem.SendText(capturedUtterance).Forget();
            }
        }

        public void StopCurrentAndScheduledActions()
        {
            _virbeActionPlayer.StopCurrentAndScheduledActions();
        }

        private async UniTask RestoreConversation(string endUserId, string roomId)
        {
            await _communicationSystem.InitializeWith(endUserId, roomId);
            ConversationStarted?.Invoke();
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
                OnBeingStateChanged?.Invoke(_currentState);
            }

            ScheduleChangeBeingStateAfterTimeoutIfNeeded();
        }

        private void ScheduleChangeBeingStateAfterTimeoutIfNeeded()
        {
            //safeguard if being is already diabled
            if (!enabled || !gameObject.activeInHierarchy)
            {
                return;
            }
            if (_autoBeingStateChangeCoroutine != null)
            {
                // Canceling previous timeout to avoid unexpected changes
                StopCoroutine(_autoBeingStateChangeCoroutine);
            }

            switch (_currentState._behaviour)
            {
                case Behaviour.Listening:
                    // make sure being is not constantly waiting for recording
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
            if (gameObject != null)
            {
                ChangeBeingState(newBehaviour);
            }
        }

        public void CallUserAction(UserAction userAction)
        {
            _currentState.lastUserAction = userAction;
            onUserAction?.Invoke(userAction);
            OnUserAction?.Invoke(userAction);
        }

        public void CallBeingActionStarted(BeingAction beingAction)
        {
            ChangeBeingState(Behaviour.PlayingBeingAction);

            _currentState.lastBeingAction = beingAction;
            onBeingAction?.Invoke(beingAction);
            OnBeingAction?.Invoke(beingAction);
        }

        public void CallBeingActionEnded(BeingAction beingAction)
        {
            ChangeBeingState(Behaviour.InConversation);
        }
    }
}