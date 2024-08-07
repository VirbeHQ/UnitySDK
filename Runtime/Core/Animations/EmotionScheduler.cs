using System;
using System.Collections.Generic;

namespace Virbe.Core.Emotions
{
    internal class EmotionScheduler
    {
        private class EmotionEntry
        {
            public Emotion Emotion { get; set; }
            public float StartTime { get; set; }
            public float EndTime { get; set; }

            public bool IsRunning { get; set; }
        }

        internal event Action<Emotion> OnEmotionStart;
        internal event Action<Emotion> OnEmotionStop;

        private List<EmotionEntry> _scheduleEmotions = new List<EmotionEntry>();
        private List<EmotionEntry> _usedEmotions = new List<EmotionEntry>();


        internal void Schedule(IEnumerable<Emotion> gestures)
        {
            foreach (Emotion gesture in gestures)
            {
                var gestureEntry = new EmotionEntry();
                gestureEntry.Emotion = gesture;
                gestureEntry.StartTime = AsSeconds(gesture.StartTime);
                gestureEntry.EndTime = AsSeconds(gesture.StartTime + gesture.Duration);
                _scheduleEmotions.Add(gestureEntry);
            }
        }

        internal void AdvanceBy(float delaTime)
        {
            foreach (var entry in _scheduleEmotions)
            {
                entry.StartTime -= delaTime;
                entry.EndTime -= delaTime;
                if (entry.EndTime <= 0 && entry.IsRunning)
                {
                    OnEmotionStop?.Invoke(entry.Emotion);
                    _usedEmotions.Add(entry);
                }
                else if (entry.StartTime <= 0 && !entry.IsRunning)
                {
                    OnEmotionStart?.Invoke(entry.Emotion);
                    entry.IsRunning = true;
                }
            }
            foreach (var entry in _usedEmotions)
            {
                _scheduleEmotions.Remove(entry);
            }
            _usedEmotions.Clear();
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