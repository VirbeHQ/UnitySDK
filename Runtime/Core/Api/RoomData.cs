using Plugins.Virbe.Core.Api;
using System;
using System.Collections.Generic;

namespace Virbe.Core
{
    public class RoomData: ConversationData
    {
        public string RoomUrl { get; }
        private string _locationId;

        public RoomData(string roomApiAccessKey, string roomUrl, List<SupportedPayload> supportedPayloads, ConnectionProtocol connectionProtocol, string locationId, string path) : base(roomApiAccessKey, supportedPayloads, connectionProtocol, path)
        {
            RoomUrl = roomUrl;
            _locationId = locationId;
        }

        internal RoomApiService CreateRoomObject(string endUserId) =>
            new RoomApiService(RoomUrl, ApiAccessKey, _locationId, endUserId);
    }
}