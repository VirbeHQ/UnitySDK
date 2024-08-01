using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using Virbe.Core.Gestures;
using Virbe.Core.Speech;

// ReSharper disable UnusedMember.Global
// ReSharper disable RedundantDefaultMemberInitializer

namespace Virbe.Core.Actions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VirbeBeing))]
    [RequireComponent(typeof(VirbeAnimatorPlayer))]
    [RequireComponent(typeof(AbstractVirbeSpeechPlayer))]
    public class VirbeActionPlayer : MonoBehaviour
    {
        [Header("Being Configuration in Scene")] [Tooltip("Delay between consecutive actions")] [SerializeField]
        protected internal float ActionPauseSeconds = 0.3f;

        [SerializeField] protected internal bool PlayWithSound = true;

        [Tooltip("Audio Source to play voice")] [SerializeField]
        protected internal AudioSource outputAudioSource;

        [Tooltip("Your model animator")] [SerializeField]
        protected internal Animator _animator;

        private VirbeBeing _virbeBeing;
        private AbstractVirbeSpeechPlayer _virbeSpeechPlayer;
        private VirbeAnimatorPlayer _virbeAnimatorPlayer;

        private ConcurrentQueue<BeingAction> _beingActionsQueue = new ConcurrentQueue<BeingAction>();

        private bool processingInProgress;
        private BeingAction? currentBeingAction;
        private Coroutine voiceEndSignalCoroutine;

        private void Awake()
        {
            // Add animation supporting components
            _virbeBeing = GetComponent<VirbeBeing>();
            _virbeAnimatorPlayer = GetComponent<VirbeAnimatorPlayer>();
            _virbeSpeechPlayer = GetComponent<AbstractVirbeSpeechPlayer>();
        }

        private void Start()
        {
            SwitchAnimator(_animator);
        }

        public void SwitchAudioSource(AudioSource audioSource)
        {
            if (outputAudioSource != null) audioSource.Stop();
            outputAudioSource = audioSource;
        }

        public void SwitchAnimator(Animator animator)
        {
            _animator = animator;
            _virbeSpeechPlayer.SetupSpeechAnimator(animator);
        }

        public void SetBeingIsSitting(bool isSitting)
        {
            _virbeAnimatorPlayer?.SwitchToSitting(isSitting);
        }

        void PlayBeingAction(BeingAction beingAction)
        {
            currentBeingAction = beingAction;

            _virbeBeing?.OnBeingActionStarted(beingAction);

            _virbeSpeechPlayer?.Play(beingAction.marks);
            if (beingAction.custom != null)
            {
                _virbeAnimatorPlayer?.SchedulePlayGestures(beingAction.custom.ExtractVirbeGestures());
                _virbeAnimatorPlayer?.SchedulePlayEmotions(beingAction.custom.ExtractVirbeEmotion());
            }

            if (PlayWithSound && beingAction.HasAudio())
            {
                var playedVoice = PlayVoice(beingAction.speech, _virbeBeing.ApiBeingConfig);
                if (playedVoice)
                {
                    voiceEndSignalCoroutine =
                        StartCoroutine(
                            AfterVoicePlayed(beingAction.GetAudioLength(_virbeBeing.ApiBeingConfig), beingAction));
                }
                else
                {
                    // TODO wait a moment before next action
                    StopBeingAction(beingAction);
                }
            }
        }

        public bool PlayVoice(byte[] audioBytes, IApiBeingConfig beingConfig)
        {
            if (outputAudioSource && audioBytes.Length > 0)
            {
                var audioClip = AudioClip.Create("clip", audioBytes.Length, beingConfig.FallbackTTSData.AudioChannels,
                    beingConfig.FallbackTTSData.AudioFrequency, false, null);
                audioClip.SetData(AudioConverter.PCMBytesToFloats(audioBytes, beingConfig.FallbackTTSData.AudioSampleBits), 0);

                outputAudioSource.Stop();
                outputAudioSource.clip = audioClip;
                outputAudioSource.Play();
                return true;
            }
            else
            {
                Debug.LogWarning("Audio data to play is empty so there is nothing to play.");
                return false;
            }
        }

        IEnumerator AfterVoicePlayed(float time, BeingAction beingAction)
        {
            yield return new WaitForSeconds(time);
            StopBeingAction(beingAction);
        }


        void StopBeingAction(BeingAction? beingAction)
        {
            processingInProgress = false;
            if (beingAction != null)
            {
                _virbeBeing?.OnBeingActionEnded((BeingAction)beingAction);
            }
        }

        public void ScheduleNewAction(BeingAction action) => _beingActionsQueue.Enqueue(action);

        private void Update()
        {
            if (processingInProgress || _beingActionsQueue.IsEmpty)
                return;

            var hasAction = _beingActionsQueue.TryDequeue(out var beingAction);
            if (hasAction)
            {
                processingInProgress = true;
                PlayBeingAction(beingAction);
            }
        }

        private void OnStateChange(BeingState beingState)
        {
            var behaviour = beingState._behaviour;
            switch (behaviour)
            {
                case Behaviour.Idle:
                    _virbeAnimatorPlayer?.SwitchToIdle();
                    break;
                case Behaviour.Focused:
                case Behaviour.InConversation:
                case Behaviour.Listening:
                case Behaviour.RequestProcessing:
                case Behaviour.RequestError:
                case Behaviour.RequestReceived:
                    //TODO what about other states
                    _virbeAnimatorPlayer?.SwitchToFocused();
                    break;
                case Behaviour.PlayingBeingAction:
                    _virbeAnimatorPlayer?.SwitchToTalking();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(behaviour), behaviour, null);
            }
        }

        private void OnEnable()
        {
            _virbeBeing.BeingStateChanged += OnStateChange;
        }

        private void OnDisable()
        {
            StopCurrentAndScheduledActions();

            _virbeBeing.BeingStateChanged+= OnStateChange;
        }

        public bool hasActionsToPlay()
        {
            return processingInProgress;
        }

        public void StopCurrentAndScheduledActions()
        {
            while (_beingActionsQueue.TryDequeue(out _))
            {
                // do nothing until queue is empty
            }

            if (voiceEndSignalCoroutine != null && currentBeingAction != null)
            {
                StopCoroutine(voiceEndSignalCoroutine);
                StopBeingAction(currentBeingAction);
                currentBeingAction = null;
            }

            if (outputAudioSource && outputAudioSource.isPlaying)
            {
                outputAudioSource.Stop();
            }

            if (_virbeSpeechPlayer != null)
            {
                _virbeSpeechPlayer.Stop();
            }

            if (_virbeAnimatorPlayer != null)
            {
                _virbeAnimatorPlayer.SwitchToFocused();
            }
        }

        public void MuteAudio(bool muteAudio)
        {
            outputAudioSource.mute = muteAudio;
            PlayWithSound = !muteAudio;
        }
    }
}