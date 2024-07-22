using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Virbe.Core.Actions;
using Virbe.Core.Emotions;
using Virbe.Core.Logger;
using Random = UnityEngine.Random;

namespace Virbe.Core.Gestures
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VirbeActionPlayer))]
    public class VirbeAnimatorPlayer : MonoBehaviour
    {
        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(VirbeAnimatorPlayer));
        private readonly GestureScheduler _gestureScheduler = new GestureScheduler();
        private readonly EmotionScheduler _emotionScheduler = new EmotionScheduler();

        private static readonly string TALK_TYPE_ANIMATOR_PARAM = "talk_type";
        private static readonly string TALKING_ANIMATOR_PARAM = "is_talking";
        private static readonly string SITTING_ANIMATOR_PARAM = "is_sitting";
        private static readonly string FOCUSED_ANIMATOR_PARAM = "is_focused";
        private VirbeActionPlayer _beingActionPlayer;
        private Coroutine talkingTypeChangeCoroutine;

        private void Awake()
        {
            _beingActionPlayer = GetComponent<VirbeActionPlayer>();
        }

        public void SchedulePlayGestures(IEnumerable<Gesture> gestures)
        {
            if (gestures != null)
            {
                var enumerable = gestures as Gesture[] ?? gestures.ToArray();
                foreach (var gesture in enumerable)
                {
                    _logger.Log($"I will schedule play for gesture: {gesture.Name}");
                }

                _gestureScheduler.Schedule(enumerable);
            }
        }

        public void SchedulePlayEmotions(IEnumerable<Emotion> emotions)
        {
            if (emotions != null)
            {
                var enumerable = emotions as Emotion[] ?? emotions.ToArray();
                foreach (var emotion in enumerable)
                {
                    _logger.Log($"I will schedule play for emotion: {emotion.Name}");
                }

                _emotionScheduler.Schedule(emotions);
            }
        }

        public void SwitchToSitting(bool isSitting)
        {
            _logger.Log($"SwitchToSitting: {isSitting}");
            if (_beingActionPlayer._animator != null)
            {
                _beingActionPlayer._animator.SetBool(SITTING_ANIMATOR_PARAM, isSitting);
            }
        }

        public void SwitchToIdle()
        {
            _logger.Log($"SwitchToIdle");
            if (_beingActionPlayer._animator != null)
            {
                _beingActionPlayer._animator.SetBool(TALKING_ANIMATOR_PARAM, false);
                _beingActionPlayer._animator.SetBool(FOCUSED_ANIMATOR_PARAM, false);
            }
        }

        public void SwitchToFocused()
        {
            _logger.Log($"SwitchToFocused");
            if (_beingActionPlayer._animator != null)
            {
                _beingActionPlayer._animator.SetBool(TALKING_ANIMATOR_PARAM, false);
                _beingActionPlayer._animator.SetBool(FOCUSED_ANIMATOR_PARAM, true);
            }
        }

        public void SwitchToTalking()
        {
            _logger.Log($"SwitchToTalking");
            if (_beingActionPlayer._animator != null)
            {
                
                if (talkingTypeChangeCoroutine != null)
                {
                    StopCoroutine(talkingTypeChangeCoroutine);
                }
                talkingTypeChangeCoroutine = StartCoroutine(ChangeTalkingAnimatorValueCoroutine(Random.value));
                _beingActionPlayer._animator.SetBool(TALKING_ANIMATOR_PARAM, true);
                _beingActionPlayer._animator.SetBool(FOCUSED_ANIMATOR_PARAM, true);
            }
        }

        IEnumerator ChangeTalkingAnimatorValueCoroutine(float targetValue)
        {
            float currentValue = _beingActionPlayer._animator.GetFloat(TALK_TYPE_ANIMATOR_PARAM);
            float time = 0;
            while (time < 1)
            {
                time += Time.deltaTime;
                _beingActionPlayer._animator.SetFloat(TALK_TYPE_ANIMATOR_PARAM,
                    Mathf.Lerp(currentValue, targetValue, time));
                yield return null;
            }
        }

        private void PlayGestureImmediately(Gesture gesture)
        {
            _logger.Log($"PlayImmediately: \"{gesture.Name}\"");
            _beingActionPlayer._animator?.SetTrigger($"gesture_{gesture.Name}");
        }

        private void PlayEmotionImmediately(Emotion emotion)
        {
            _logger.Log($"PlayImmediately: \"{emotion.Name}\"");
            if (_beingActionPlayer._animator != null)
            {
                _beingActionPlayer._animator.SetBool($"emotion_{emotion.Name}", true);
            }
        }

        private void StopEmotionImmediately(Emotion emotion)
        {
            _logger.Log($"StopImmediately: \"{emotion.Name}\"");
            if (_beingActionPlayer._animator != null)
            {
                _beingActionPlayer._animator.SetBool($"emotion_{emotion.Name}", false);
            }
        }


        private void Update()
        {
            _gestureScheduler.AdvanceBy(Time.deltaTime);
            _emotionScheduler.AdvanceBy(Time.deltaTime);
        }

        private void OnEnable()
        {
            _gestureScheduler.OnGestureStart += PlayGestureImmediately;
            _emotionScheduler.OnEmotionStart += PlayEmotionImmediately;
            _emotionScheduler.OnEmotionStop += StopEmotionImmediately;
        }

        private void OnDisable()
        {
            _gestureScheduler.OnGestureStart -= PlayGestureImmediately;
            _emotionScheduler.OnEmotionStart -= PlayEmotionImmediately;
            _emotionScheduler.OnEmotionStop -= StopEmotionImmediately;
        }
    }
}