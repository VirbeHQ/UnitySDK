using System;

namespace Virbe.Core.Api
{
    public class VirbeUserSession
    {
        public string UserId { get; private set; }
        public string ConversationId { get; private set; }

        public VirbeUserSession(string endUserId = null, string conversationId = null)
        {
            UserId = endUserId ?? Guid.NewGuid().ToString();
            ConversationId = conversationId;
        }

        public void UpdateSession(string endUserId, string conversationId)
        {
            UserId = endUserId;
            ConversationId = conversationId;
        }
    }
}