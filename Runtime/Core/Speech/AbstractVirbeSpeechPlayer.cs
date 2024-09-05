using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Virbe.Core.Actions;
using Virbe.Core.Data;
using Virbe.Core.Logger;
using Virbe.Core.Utils;

namespace Virbe.Core.Speech
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VirbeActionPlayer))]
    public abstract class AbstractVirbeSpeechPlayer : MonoBehaviour
    {
        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(AbstractVirbeSpeechPlayer));

        // Hack if not set to small value then blendshapes positions are not reset - maybe it's Unity Engine bug?
        private readonly float CLEAR_INPUT_VALUE = 0.0001f;

        [SerializeField] protected VisemeConfig[] visemesConfiguration;

        [SerializeField] protected AnimationCurve visemeInAnimationCurve = AnimationCurve.EaseInOut(0.0f, 0.0f, 1.0f, 0.5f);

        [SerializeField] protected AnimationCurve visemeOutAnimationCurve = AnimationCurve.EaseInOut(0.0f, 0.5f, 1.0f, 0f);

        [Range(0f, 1f)] [SerializeField] protected float volumeMultiplier = 0.7f;

        // Fade Time for start/stop each Phoneme. Clamped to time distance between Phonemes (in seconds)
        [Range(0.1f, 0.3f)] [SerializeField] protected float PhonemeInTime = 0.1f;
        [Range(0.1f, 0.3f)] [SerializeField] protected float PhonemeFadeTime = 0.2f;

        private string[] visemeMappingDict = new string[0];

        private PlayableGraph playableGraph;
        private AnimationLayerMixerPlayable mixerPlayable;

        private List<Mark> _currentVisemes;
        private int _currentVisemeIndex;
        private Mark _prevViseme;
        private Mark _nextViseme;
        private float _currentSpeechTime;
        private float _speechDuration;

        protected virtual void OnDestroy()
        {
            playableGraph.Destroy();
        }

        protected virtual void Update()
        {
            if (_currentVisemes != null)
            {
                AdvanceVisemes(Time.deltaTime);
            }
        }

        protected abstract AnimationLayerMixerPlayable CreateAnimationLayerMixerPlayable(PlayableGraph playableGraph,
            int inputCount);

        public void SetupSpeechAnimator(Animator beingAnimator)
        {
            if (beingAnimator == null || visemesConfiguration == null)
            {
                return;
            }

            if (!default(PlayableGraph).Equals(playableGraph))
                playableGraph.Destroy();

            // Creates the graph, the mixer and binds them to the Animator.
            playableGraph = PlayableGraph.Create();
            var playableOutput = AnimationPlayableOutput.Create(playableGraph, "SpeechLipSync", beingAnimator);
            var visemesCount = visemesConfiguration.Length;

            mixerPlayable = CreateAnimationLayerMixerPlayable(playableGraph, visemesCount);
            mixerPlayable.SetLayerAdditive(0, false);
            playableOutput.SetSourcePlayable(mixerPlayable);

            visemeMappingDict = new string[visemesCount];

            for (var index = 0; index < visemesCount; index++)
            {
                var viseme = visemesConfiguration[index];
                visemeMappingDict[index] = viseme.name.GetAttribute<DescriptorAttribute>().MappingName;

                // Creates AnimationClipPlayable and connects them to the mixer.
                var clipPlayable = AnimationClipPlayable.Create(playableGraph, viseme.clip);
                playableGraph.Connect(clipPlayable, 0, mixerPlayable, index);
                mixerPlayable.SetInputWeight(index, 0);
            }

            playableGraph.Play();
        }


        public void Play(List<Mark> marks)
        {
            for (int i = 0; i < visemeMappingDict.Length; i++)
            {
                mixerPlayable.SetInputWeight(i, 0);
            }

            CleanUpPrevVisemes();
            _speechDuration = AsSeconds(marks.Last().Time) + PhonemeFadeTime;
            _currentVisemes = marks;
            if (marks.Count > 0)
            {
                _nextViseme = marks[0];
            }
        }

        private void CleanUpPrevVisemes()
        {
            _currentVisemes = null;
            _currentVisemeIndex = 0;
            _currentSpeechTime = 0;
            _prevViseme = null;
            _nextViseme = null;
        }

        private void AdvanceVisemes(float time)
        {
            _currentSpeechTime += time;


            if (_currentSpeechTime > _speechDuration)
            {
                CleanUpPrevVisemes();
            }
            else if (_nextViseme != null && _currentSpeechTime > AsSeconds(_nextViseme.Time))
            {
                _currentVisemeIndex++;
                var nextVisemeCandidate = findNextViseme();
                if (_prevViseme != null && _nextViseme.Value != _prevViseme.Value) //do not zero out same viseme
                {
                    // Quick decay would be better
                    SetAnimationValue(_prevViseme.Value, CLEAR_INPUT_VALUE);
                }


                _prevViseme = _nextViseme;
                _nextViseme = nextVisemeCandidate;
            }

            float prevVisemeTime = _prevViseme != null ? AsSeconds(_prevViseme.Time) : 0.0f;
            float nextVisemeTime = _nextViseme != null ? AsSeconds(_nextViseme.Time) : _speechDuration;
            if ((nextVisemeTime - prevVisemeTime) > PhonemeFadeTime)
                prevVisemeTime = nextVisemeTime - PhonemeInTime;

            // TODO decide if next Viseme is vowel?
            float progress = Mathf.Clamp((_currentSpeechTime - prevVisemeTime) / (nextVisemeTime - prevVisemeTime),
                0.0f, 1.0f);


            // if ((NextVisemeTime - PrevVisemeTime) > PhonemeFadeTime)
            //     PrevVisemeTime = NextVisemeTime - PhonemeFadeTime;
            // float fadeTime = _nextViseme && _nextViseme.viseme <= EViseme::VIS_LastConsowel ? ConsonantsInTime : VowelsInTime;
            // float NextVisemeMulitplier = Mathf.Clamp((NextVisemeTime - PrevVisemeTime) / fadeTime, 0.0f, 1.0f);

            //clear
            for (int i = 0; i < visemeMappingDict.Length; i++)
            {
                // Quick decay would be better
                mixerPlayable.SetInputWeight(i, CLEAR_INPUT_VALUE);
            }

            //do the lerp
            if (_prevViseme != null)
            {
                // Slow decay would be better
                VisemeOutUpdate(_prevViseme.Value, (progress) * volumeMultiplier);
            }

            if (_nextViseme != null)
            {
                // Quick decay would be better
                VisemeInUpdate(_nextViseme.Value, progress * volumeMultiplier);
            }
        }

        private Mark findNextViseme()
        {
            return _currentVisemeIndex < _currentVisemes.Count ? _currentVisemes[_currentVisemeIndex] : null;
        }

        private void VisemeInUpdate(string viseme, float progress)
        {
            progress = visemeInAnimationCurve.Evaluate(progress) * volumeMultiplier;
            SetAnimationValue(viseme, progress);
        }

        private void VisemeOutUpdate(string viseme, float progress)
        {
            progress = visemeOutAnimationCurve.Evaluate(progress) * volumeMultiplier;
            SetAnimationValue(viseme, progress);
        }

        private void SetAnimationValue(string viseme, float progress)
        {
            var visemeIndex = Array.IndexOf(visemeMappingDict, viseme);
            if (visemeIndex != -1) mixerPlayable.SetInputWeight(visemeIndex, progress);
        }

        private static float AsSeconds(int millis)
        {
            return (float)millis / 1000;
        }

        public void Stop()
        {
            CleanUpPrevVisemes();

            for (int i = 0; i < visemeMappingDict.Length; i++)
            {
                mixerPlayable.SetInputWeight(i, CLEAR_INPUT_VALUE);
            }
        }
    }
}