using System;
using System.Collections.Generic;

namespace Virbe.Core.Gestures
{
    internal class GestureScheduler
    {
        private class GestureEntry
        {
            public Gesture Gesture { get; set; }
            public float StartTime { get; set; }
            public float EndTime { get; set; }

            public bool IsRunning { get; set; }
        }

        internal event Action<Gesture> OnGestureStart;
        internal event Action<Gesture> OnGestureEnd;

        private List<GestureEntry> _scheduleGestures = new List<GestureEntry>();
        private List<GestureEntry>  _usedGestures = new List<GestureEntry>();

        internal void Schedule(IEnumerable<Gesture> gestures)
        {
            foreach (Gesture gesture in gestures)
            {
                var gestureEntry = new GestureEntry();
                gestureEntry.Gesture = gesture;
                gestureEntry.StartTime = AsSeconds(gesture.StartTime);
                gestureEntry.EndTime = AsSeconds(gesture.StartTime + gesture.Duration);
                _scheduleGestures.Add(gestureEntry);
            }
        }

        internal void AdvanceBy(float delaTime)
        {
            foreach(var entry in _scheduleGestures)
            {
                entry.StartTime -= delaTime;
                entry.EndTime -= delaTime;
                if(entry.EndTime <= 0 && entry.IsRunning)
                {
                    OnGestureEnd?.Invoke(entry.Gesture);
                    _usedGestures.Add(entry);
                }
                else if(entry.StartTime <= 0 && !entry.IsRunning)
                {
                    OnGestureStart?.Invoke(entry.Gesture);
                    entry.IsRunning = true;
                }
            }
            foreach (var entry in _usedGestures)
            {
                _scheduleGestures.Remove(entry);
            }
            _usedGestures.Clear();
        }

        private static float AsSeconds(int millis)
        {
            return (float)millis / 1000;
        }
    }
}