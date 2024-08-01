using System;
using System.Collections.Generic;
using Plugins.Virbe.Core.Api;
using Newtonsoft.Json;
using System.Linq;

namespace Virbe.Core
{
    [Serializable]
    public class ApiBeingConfigv3 : IApiBeingConfig
    {
        [JsonProperty("schema")]
        public string Schema { get; set; }

        [JsonProperty("baseUrl")]
        public string BaseUrl { get; set; }

        [JsonProperty("location")]
        public Location Location { get; set; }

        [JsonProperty("engines")]
        public Engines Engines { get; set; }

        string IApiBeingConfig.BaseUrl => BaseUrl;

        EngineType IApiBeingConfig.ConversationEngine => GetEngineType(Engines.Conversation);

        TTSData IApiBeingConfig.FallbackTTSData => _ttsData;
        private TTSData _ttsData;

        STTData IApiBeingConfig.FallbackSTTData => _sttData;
        private STTData _sttData;

        List<ConversationData> IApiBeingConfig.ConversationData => _conversationData;
        private List<ConversationData> _conversationData = new List<ConversationData>();
        bool IApiBeingConfig.HasRoom => _hasRoom;
        private bool _hasRoom;

        internal void Initialize()
        {
            _hasRoom = false;
            foreach (var convHandler in Engines?.Conversation?.ConnectionHandlers ?? new List<ConnectionHandler>())
            {
                ConversationData handler = null;
                var payloads = GetPayloads(convHandler);
                if (payloads.Contains(SupportedPayload.RoomMessage))
                {
                    handler = new RoomData(convHandler.ApiAccessKey, convHandler.Url, payloads, GetProtocol(convHandler), Location.Id);
                    _hasRoom = true;
                }
                else
                {
                    handler = new ConversationData(convHandler.ApiAccessKey, GetPayloads(convHandler), GetProtocol(convHandler));
                }
                _conversationData.Add(handler);
            }

            if (Engines?.Stt != null)
            {
                var connectionHandler = Engines.Stt.ConnectionHandlers.FirstOrDefault();
                if (connectionHandler != null)
                {
                    _sttData = new STTData(GetProtocol(connectionHandler), connectionHandler.Path);
                }
            }

            if (Engines?.Tts != null)
            {
                var mainAudioParameters = Engines.Tts.SupportedAudioParameters.FirstOrDefault();
                var connectionHandler = Engines.Tts.ConnectionHandlers.FirstOrDefault();
                if (mainAudioParameters != null && connectionHandler != null)
                {
                    _ttsData = new TTSData(GetProtocol(connectionHandler), mainAudioParameters.Channels, mainAudioParameters.Frequency, mainAudioParameters.SampleBits, connectionHandler.Path);
                }
            }
        }
        private List<SupportedPayload> GetPayloads(ConnectionHandler connectionHandler)
        {
            var result = new List<SupportedPayload>();
            foreach(var payload in connectionHandler.SupportedPayloads)
            {
                result.Add(GetPayloadType(payload));
            }
            return result;
        }
        private SupportedPayload GetPayloadType(string value) => value switch
        {
            "room-message" => SupportedPayload.RoomMessage,
            "conversation-message" => SupportedPayload.ConversationMessage,
            "speech-stream" => SupportedPayload.SpeechStream,

            _ => throw new ArgumentOutOfRangeException(nameof(SupportedPayload), value, null)
        };

        private ConnectionProtocol GetProtocol(ConnectionHandler handler) => handler.Protocol switch
        {
            "local" => ConnectionProtocol.local,
            "http" => ConnectionProtocol.http,
            "ws" => ConnectionProtocol.ws,
            "socket-io" => ConnectionProtocol.socket_io,
            "ws-endless" => ConnectionProtocol.wsEndless,
            _ => throw new ArgumentOutOfRangeException(nameof(ConnectionProtocol), handler.Protocol, null)
        };
        private EngineType GetEngineType(Engine engine) => engine?.EngineType switch
        {
            "room" => EngineType.Room,
            "virbe-ai" => EngineType.VirbeAi,
            "azure-cognitive" => EngineType.AzureCognitive,
            _ => throw new NotImplementedException()
        };
    }


    public class Location
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("channel")]
        public string Channel { get; set; }
    }

    public class Engines
    {
        [JsonProperty("conversation")]
        public Engine Conversation { get; set; }

        [JsonProperty("stt")]
        public Engine Stt { get; set; }

        [JsonProperty("tts")]
        public Engine Tts { get; set; }
    }

    public class Engine
    {
        [JsonProperty("engine")]
        public string EngineType { get; set; }

        [JsonProperty("connectionHandlers")]
        public List<ConnectionHandler> ConnectionHandlers { get; set; }

        [JsonProperty("supportedAudioParameters")]
        public List<AudioParameters> SupportedAudioParameters { get; set; }
    }

    public class ConnectionHandler
    {
        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("apiAccessKey")]
        public string ApiAccessKey { get; set; }

        [JsonProperty("protocol")]
        public string Protocol { get; set; }

        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; }

        [JsonProperty("supportedPayloads")]
        public List<string> SupportedPayloads { get; set; }
    }

    public class AudioParameters
    {
        [JsonProperty("channels")]
        public int Channels { get; set; }

        [JsonProperty("frequency")]
        public int Frequency { get; set; }

        [JsonProperty("sampleBits")]
        public int SampleBits { get; set; }

        [JsonProperty("format")]
        public string Format { get; set; }
    }
}