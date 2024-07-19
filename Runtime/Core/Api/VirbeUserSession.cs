using System;

namespace Virbe.Core.Api
{
    public class VirbeUserSession
    {
        private string _endUserId;
        private string _roomId;

        public VirbeUserSession(string endUserId = null, string roomId = null)
        {
            this._endUserId = endUserId ?? Guid.NewGuid().ToString();
            this._roomId = roomId;
        }

        public string EndUserId => _endUserId;

        public string RoomId => _roomId;

        public void UpdateSession(string endUserId, string roomId)
        {
            this._endUserId = endUserId;
            this._roomId = roomId;
        }

        public bool HasRoomId()
        {
            return !string.IsNullOrEmpty(RoomId);
        }
    }
}