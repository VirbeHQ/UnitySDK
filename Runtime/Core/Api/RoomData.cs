using Virbe.Core.Data;
using Virbe.Core.RoomApi;

namespace Virbe.Core
{
    public class RoomData: ConversationData
    {
        public string RoomUrl { get; }
        internal string ApiAccessKey { get; }

        private string _locationId;

        public RoomData(string roomApiAccessKey, string roomUrl, ConnectionProtocol connectionProtocol, string locationId, string path) : base( connectionProtocol, path)
        {
            RoomUrl = roomUrl;
            _locationId = locationId;
            ApiAccessKey = roomApiAccessKey;
        }

        internal RoomApiService CreateRoomObject(string endUserId) =>
            new RoomApiService(RoomUrl, ApiAccessKey, _locationId, endUserId);
    }
}