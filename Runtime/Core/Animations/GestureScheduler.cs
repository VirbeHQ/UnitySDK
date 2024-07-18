using System.Collections.Generic;
using System.Linq;
using RSG;

namespace Virbe.Core.Gestures
{
    internal class GestureScheduler
    {
        internal delegate void OnGestureStartDelegate(Gesture gesture);
        internal delegate void OnGestureEndDelegate(Gesture gesture);


        internal OnGestureStartDelegate OnGestureStart;
        internal OnGestureEndDelegate OnGestureEnd;

        private List<PromiseTimer> _scheduleTimers = new List<PromiseTimer>();

        internal void Schedule(IEnumerable<Gesture> gestures)
        {
            _scheduleTimers.AddRange(gestures
                .SelectMany(gesture =>
                {
                    var startTimer = new PromiseTimer();
                    startTimer
                        .WaitFor(AsSeconds(millis: gesture.StartTime))
                        .Then(() => { StartGesturePlay(gesture); });
                    
                    var stopTimer = new PromiseTimer();
                    stopTimer
                        .WaitFor(AsSeconds(millis: gesture.StartTime + gesture.Duration))
                        .Then(() =>
                        {
                            EndGesturePlay(gesture);
                        });

                    return AsEnumerable(startTimer, stopTimer);
                })
                .ToList());
        }

        private void EndGesturePlay(Gesture gesture)
        {
            OnGestureEnd?.Invoke(gesture);
        }

        private void StartGesturePlay(Gesture gesture)
        {
            OnGestureStart?.Invoke(gesture);
        }

        internal void AdvanceBy(float seconds)
        {
            _scheduleTimers.ForEach(pt => pt.Update(seconds));
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