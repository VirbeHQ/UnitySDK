using System.Collections.Generic;

namespace Virbe.Core
{
    public class ConversationData
    {
        public List<SupportedPayload> SupportedPayloads { get; }
        public ConnectionProtocol ConnectionProtocol { get; }
        internal string ApiAccessKey { get; }
        internal string Path { get; }

        public ConversationData(string apiAccessKey, List<SupportedPayload> supportedPayloads, ConnectionProtocol connectionProtocol, string path)
        {
            SupportedPayloads = supportedPayloads;
            ApiAccessKey = apiAccessKey;
            ConnectionProtocol = connectionProtocol;
            Path = path;
        }
    }
}