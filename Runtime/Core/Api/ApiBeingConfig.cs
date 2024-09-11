using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Virbe.Core.Data
{
    [Serializable]
    public class ApiBeingConfig: IApiBeingConfig
    {
        public string beingStatus { get; set; }
        public RoomConfig room { get; set; }
        public LocationConfig location { get; set; }
        public SttConfig sttConfig { get; set; }
        public TtsConfig ttsConfig { get; set; }
        public HostConfig host { get; set; }

        public ApiBeingConfig()
        {
            sttConfig = new SttConfig();
            ttsConfig = new TtsConfig();
            host = new HostConfig();
        }
        EngineType IApiBeingConfig.ConversationEngine => EngineType.Room;

        TTSData IApiBeingConfig.FallbackTTSData => _ttsData;
        private TTSData _ttsData;

        STTData IApiBeingConfig.FallbackSTTData => _sttData;
        private STTData _sttData;

        List<ConversationData> IApiBeingConfig.ConversationData => _conversationData;
        private List<ConversationData> _conversationData = new List<ConversationData>();

        string IApiBeingConfig.LocationId => location.id;

        AvatarData IApiBeingConfig.AvatarData => _avatarData;
        private AvatarData _avatarData;

        internal void Initialize()
        {
            if (room != null)
            {
                var roomHandler = new RoomData(room?.roomApiAccessKey, room?.roomUrl, ConnectionProtocol.http, location.id, room?.roomUrl);
                _conversationData.Add(roomHandler);
            }

            if (sttConfig != null)
            {
                _sttData = new STTData(ConnectionProtocol.http, string.Empty);
            }

            if (ttsConfig != null)
            {
                _ttsData = new TTSData(ConnectionProtocol.http, ttsConfig.audioChannels, ttsConfig.audioFrequency, ttsConfig.audioSampleBits, string.Empty);
            }
            _avatarData = new AvatarData() { AvatarUrl = host.character.mainFile };
        }

        void IApiBeingConfig.Localize(LocalizationData data)
        {
        }

        [Serializable]
        public class PresenterConfig
        {
            [JsonProperty] protected internal bool enabled = false;
            [JsonProperty] protected internal string assetsPath;
            [JsonProperty] protected internal string welcomeNode;
            [JsonProperty] protected internal string defaultNode;
        }

        [Serializable]
        public class ChatConfig
        {
            [JsonProperty] protected internal bool enabled = false;
            [JsonProperty] protected internal string origin;
            [JsonProperty] protected internal string chatUrl;
            [JsonProperty] protected internal string chatApiAccessKey;
        }
        
        [Serializable]
        public class RoomConfig
        {
            [JsonProperty] protected internal bool enabled = false;
            [JsonProperty] protected internal string roomUrl;
            [JsonProperty] protected internal string roomApiAccessKey;
        }
        
        [Serializable]
        public class LocationConfig
        {
            [JsonProperty] protected internal string id;
            [JsonProperty] protected internal string name;
            [JsonProperty] protected internal string channel;
        }

        [Serializable]
        public class SttConfig
        {
            [JsonProperty] protected internal string engine = "default";
            [JsonProperty] public int recordingFrequency { get; set; } = 16000;
            [JsonProperty] public int recordingChannels { get; set; } = 1;
        }

        [Serializable]
        public class TtsConfig
        {
            [JsonProperty] protected internal string engine = "default";
            [JsonProperty] public int audioFrequency = 16000;
            [JsonProperty] public int audioSampleBits = 16;
            [JsonProperty] public int audioChannels = 1;
            [JsonProperty] protected internal string audioType = "pcm";
        }

        [Serializable]
        public class HostConfig
        {
            [JsonProperty] public string type { get; set; } = "rpm";
            [JsonProperty] public Character character { get; set; }
        }

        [Serializable]
        public class Character
        {
            [JsonProperty] public string mainFile { get; set; }
        }
    }
}