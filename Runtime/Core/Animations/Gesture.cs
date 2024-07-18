using Newtonsoft.Json.Linq;

namespace Virbe.Core.Gestures
{
    public struct Gesture
    {
        public readonly string Name;
        public readonly int StartTime;
        public readonly int Duration;

        internal Gesture(JToken dict)
        {
            Name = dict.Value<string?>("name") ?? "";
            StartTime = dict.Value<int?>("start_time") ?? 0;
            Duration = dict.Value<int?>("duration") ?? 0;
        }
    }
}