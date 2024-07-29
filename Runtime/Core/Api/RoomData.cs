using System;

namespace Virbe.Core
{
    public class RoomData
    {
        public string RoomApiAccessKey { get; }
        public string RoomUrl { get; }
        public bool Enabled { get; }
        public string HostDomain { get; }

        public RoomData(string roomApiAccessKey, string roomUrl, bool roomEnabled)
        {
            RoomApiAccessKey = roomApiAccessKey;
            RoomUrl = roomUrl;
            Enabled = roomEnabled;
            HostDomain = !string.IsNullOrEmpty(RoomUrl) ?
               new Uri(RoomUrl).GetLeftPart(UriPartial.Authority) :
               null;
        }
    }
}