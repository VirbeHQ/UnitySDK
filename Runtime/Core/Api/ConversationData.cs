using System.Collections.Generic;

namespace Virbe.Core
{
    public class ConversationData
    {
        public List<SupportedPayload> SupportedPayloads { get; }
        public ConnectionProtocol ConnectionProtocol { get; }
        internal string ApiAccessKey { get; }

        public ConversationData(string apiAccessKey, List<SupportedPayload> supportedPayloads, ConnectionProtocol connectionProtocol)
        {
            SupportedPayloads = supportedPayloads;
            ApiAccessKey = apiAccessKey;
            ConnectionProtocol = connectionProtocol;
        }
    }
}