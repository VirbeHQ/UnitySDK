using Newtonsoft.Json;
using Plugins.Virbe.Core.Api;
using System;

namespace Virbe.Core
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

        public string HostDomain => !string.IsNullOrEmpty(room?.roomUrl) ? new Uri(room?.roomUrl).GetLeftPart(UriPartial.Authority) : null;
        string IApiBeingConfig.BaseUrl => HostDomain;
        int IApiBeingConfig.AudioChannels => ttsConfig.audioChannels;
        int IApiBeingConfig.AudioFrequency => ttsConfig.audioFrequency;
        int IApiBeingConfig.AudioSampleBits => ttsConfig.audioSampleBits;
        bool IApiBeingConfig.HasRoom => room != null;
        string IApiBeingConfig.SttPath => string.Empty;
        SttConnectionProtocol IApiBeingConfig.SttProtocol => SttConnectionProtocol.http;

        EngineType IApiBeingConfig.EngineType => EngineType.Room;

        RoomData IApiBeingConfig.RoomData
        {
            get
            {
                if (_roomData == null && room != null)
                {
                    _roomData = new RoomData(room?.roomApiAccessKey, room?.roomUrl, room?.enabled ?? false);
                }
                return _roomData;
            }
        }
        private RoomData _roomData;
        public bool HasValidHostDomain()
        {
            return !string.IsNullOrEmpty(HostDomain);
        }

        public bool HasValidApiAccessKey()
        {
            return !string.IsNullOrEmpty(room?.roomApiAccessKey);
        }
        RoomApiService IApiBeingConfig.CreateRoomObject(string endUserId) => new RoomApiService(room.roomUrl, room.roomApiAccessKey, location.id, endUserId);

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