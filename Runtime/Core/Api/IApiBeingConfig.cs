using System.Collections.Generic;

namespace Virbe.Core
{
    public interface IApiBeingConfig
    {
        string BaseUrl { get; }
        internal string LocationId {  get; }
        EngineType ConversationEngine { get; }
        List<ConversationData> ConversationData { get; }
        AvatarData AvatarData { get; }
        bool HasRoom { get; }

        TTSData FallbackTTSData { get; }
        STTData FallbackSTTData { get; }
    }
}