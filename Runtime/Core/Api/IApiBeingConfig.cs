using Plugins.Virbe.Core.Api;
using System.Collections.Generic;

namespace Virbe.Core
{
    public interface IApiBeingConfig
    {
        string BaseUrl { get; }
        EngineType ConversationEngine { get; }
        List<ConversationData> ConversationData { get; }

        bool HasRoom { get; }

        TTSData FallbackTTSData { get; }
        STTData FallbackSTTData { get; }
    }
}