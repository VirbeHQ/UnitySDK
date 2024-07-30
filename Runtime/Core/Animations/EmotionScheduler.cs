using System.Collections.Generic;
using System.Linq;
using RSG;

namespace Virbe.Core.Emotions
{
    internal class EmotionScheduler
    {
        internal delegate void OnEmotionStartDelegate(Emotion emotion);

        internal delegate void OnEmotionStopDelegate(Emotion emotion);

        internal OnEmotionStartDelegate OnEmotionStart;
        internal OnEmotionStopDelegate OnEmotionStop;

        private List<PromiseTimer> _scheduleTimers = new List<PromiseTimer>();

        internal void Schedule(IEnumerable<Emotion> emotions)
        {
            _scheduleTimers = emotions
                .SelectMany(emotion =>
                {
                    var startTimer = new PromiseTimer();
                    startTimer
                        .WaitFor(AsSeconds(millis: emotion.StartTime))
                        .Then(() => { OnEmotionStart?.Invoke(emotion); });

                    var stopTimer = new PromiseTimer();
                    stopTimer
                        .WaitFor(AsSeconds(millis: emotion.StartTime + emotion.Duration))
                        .Then(() => { OnEmotionStop?.Invoke(emotion); });

                    return AsEnumerable(startTimer, stopTimer);
                })
                .ToList();
        }

        internal void AdvanceBy(float seconds)
        {
            foreach (var pt in _scheduleTimers) pt.Update(seconds);
        }

        private static float AsSeconds(int millis)
        {
            return (float)millis / 1000;
        }

        private static IEnumerable<T> AsEnumerable<T>(params T[] items)
        {
            return items;
        }
    }
}