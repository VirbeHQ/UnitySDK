using System;
using System.Collections.Generic;
using Plugins.Virbe.Core.Api;
using Newtonsoft.Json;

namespace Virbe.Core
{
    [Serializable]
    public class ApiBeingConfigv3 : IApiBeingConfig
    {
        [JsonProperty("schema")]
        public string Schema { get; set; }
        [JsonProperty("baseUrl")]
        public string BaseUrl { get; set; }
        [JsonProperty("conversation")]
        public AiEngine Conversation { get; set; }
        [JsonProperty("location")]
        public Location Location { get; set; }
        [JsonProperty("stt")]
        public STT Stt { get; set; }
        [JsonProperty("tts")]
        public TTS Tts { get; set; }
        [JsonProperty("configuration")]
        public Configuration Configuration { get; set; }

        string IApiBeingConfig.BaseUrl => BaseUrl;

        EngineType IApiBeingConfig.EngineType => Conversation?.Engine switch
        {
            "room" => EngineType.Room,
            "virbe-ai" => EngineType.VirbeAi,
            _ => throw new NotImplementedException()
        };

        RoomData IApiBeingConfig.RoomData
        {
            get
            {
                if(_roomData == null && Conversation?.Room != null)
                {
                    _roomData = new RoomData(Conversation?.Room?.RoomApiAccessKey, Conversation?.Room?.RoomUrl, Conversation?.Room?.Enabled ?? false);
                }
                return _roomData;
            }
        }
        int IApiBeingConfig.AudioChannels => Tts.AudioChannels;

        int IApiBeingConfig.AudioFrequency => Tts.AudioFrequency;

        int IApiBeingConfig.AudioSampleBits => Tts.AudioSampleBits;

        bool IApiBeingConfig.HasRoom => Conversation?.Room != null;

        SttConnectionProtocol IApiBeingConfig.SttProtocol => Stt.Protocol switch
        {
            "local" => SttConnectionProtocol.local,
            "http" => SttConnectionProtocol.http,
            "sse" => SttConnectionProtocol.sse,
            "ws" => SttConnectionProtocol.ws,
            "socket-io" => SttConnectionProtocol.socket_io,

            _ => throw new ArgumentOutOfRangeException(nameof(SttConnectionProtocol), Stt.Protocol, null)
        };
        TtsConnectionProtocol IApiBeingConfig.TtsConnectionProtocol => Tts.Protocol switch
        {
            "http" => TtsConnectionProtocol.http,
            "room" => TtsConnectionProtocol.room,
            _ => throw new ArgumentOutOfRangeException(nameof(TtsConnectionProtocol), Tts.Protocol, null)
        };
        string IApiBeingConfig.SttPath => Stt.Path;

        private RoomData _roomData;

        RoomApiService IApiBeingConfig.CreateRoomObject(string endUserId) 
            => new RoomApiService(Conversation?.Room?.RoomUrl, 
                Conversation?.Room?.RoomApiAccessKey,
                Location.Id,
                endUserId);
    }
    public class AiEngine
    {
        [JsonProperty("engine")]
        internal string Engine { get; set; }

        [JsonProperty("room")]
        public Room Room { get; set; }
    }

    public class Room
    {
        [JsonProperty("enabled")]
        internal bool Enabled { get; set; }

        [JsonProperty("roomUrl")]
        internal string RoomUrl { get; set; }

        [JsonProperty("roomApiAccessKey")]
        internal string RoomApiAccessKey { get; set; }

        [JsonProperty("protocol")]
        internal string Protocol { get; set; }

        [JsonProperty("path")]
        internal string Path { get; set; }
    }

    public class Location
    {
        [JsonProperty("id")]
        internal string Id { get; set; }

        [JsonProperty("name")]
        internal string Name { get; set; }

        [JsonProperty("predefined")]
        internal bool Predefined { get; set; }

        [JsonProperty("pingMonitoring")]
        internal bool PingMonitoring { get; set; }

        [JsonProperty("channel")]
        internal string Channel { get; set; }

        [JsonProperty("createdAt")]
        internal DateTime CreatedAt { get; set; }

        [JsonProperty("createdBy")]
        internal object CreatedBy { get; set; }

        [JsonProperty("modifiedAt")]
        internal DateTime ModifiedAt { get; set; }

        [JsonProperty("modifiedBy")]
        internal object ModifiedBy { get; set; }

        [JsonProperty("archivedAt")]
        internal object ArchivedAt { get; set; }
    }

    public class STT
    {
        [JsonProperty("engine")]
        internal string Engine { get; set; }

        [JsonProperty("recordingChannels")]
        internal int RecordingChannels { get; set; }

        [JsonProperty("recordingFrequency")]
        internal int RecordingFrequency { get; set; }

        [JsonProperty("protocol")]
        internal string Protocol { get; set; }

        [JsonProperty("path")]
        internal string Path { get; set; }
    }

    public class TTS
    {
        [JsonProperty("engine")]
        internal string Engine { get; set; }

        [JsonProperty("audioChannels")]
        internal int AudioChannels { get; set; }

        [JsonProperty("audioFrequency")]
        internal int AudioFrequency { get; set; }

        [JsonProperty("audioSampleBits")]
        internal int AudioSampleBits { get; set; }

        [JsonProperty("audioType")]
        internal string AudioType { get; set; }

        [JsonProperty("protocol")]
        internal string Protocol { get; set; }

        [JsonProperty("path")]
        internal string Path { get; set; }
    }

    public class Configuration
    {
        [JsonProperty("host")]
        internal Host Host { get; set; }

        [JsonProperty("environment")]
        internal Environment Environment { get; set; }

        [JsonProperty("configuration")]
        internal Config Config { get; set; }
    }

    public class Host
    {
        [JsonProperty("type")]
        internal string Type { get; set; }

        [JsonProperty("collapsedThumbnailFile")]
        internal string CollapsedThumbnailFile { get; set; }

        [JsonProperty("sceneLoadingPlaceholderFile")]
        internal string SceneLoadingPlaceholderFile { get; set; }

        [JsonProperty("backgroundGradient")]
        internal BackgroundGradient BackgroundGradient { get; set; }

        [JsonProperty("camera")]
        internal Camera Camera { get; set; }

        [JsonProperty("character")]
        internal Character Character { get; set; }

        [JsonProperty("animations")]
        internal Animations Animations { get; set; }

        [JsonProperty("joints")]
        internal Joints Joints { get; set; }
    }

    public class BackgroundGradient
    {
        [JsonProperty("color1Hex")]
        internal string Color1Hex { get; set; }

        [JsonProperty("color2Hex")]
        internal string Color2Hex { get; set; }
    }

    public class Camera
    {
        [JsonProperty("fullShot")]
        internal CameraPosition FullShot { get; set; }

        [JsonProperty("mediumCloseup")]
        internal CameraPosition MediumCloseup { get; set; }
    }

    public class CameraPosition
    {
        [JsonProperty("positionXyzFloat")]
        internal float[] PositionXyzFloat { get; set; }

        [JsonProperty("targetXyzFloat")]
        internal float[] TargetXyzFloat { get; set; }
    }

    public class Character
    {
        [JsonProperty("mainFile")]
        internal string MainFile { get; set; }
    }

    public class Animations
    {
        [JsonProperty("basePath")]
        internal string BasePath { get; set; }

        [JsonProperty("animationFiles")]
        internal Dictionary<string, AnimationFile> AnimationFiles { get; set; }

        [JsonProperty("gestureConfigFile")]
        internal string GestureConfigFile { get; set; }

        [JsonProperty("poiConfigFile")]
        internal string PoiConfigFile { get; set; }
    }

    public class AnimationFile
    {
        [JsonProperty("file")]
        internal string File { get; set; }

        [JsonProperty("clipsFrom")]
        internal float? ClipsFrom { get; set; }

        [JsonProperty("clipsTo")]
        internal float? ClipsTo { get; set; }
    }

    public class Joints
    {
        [JsonProperty("lookTracker")]
        internal string LookTracker { get; set; }

        [JsonProperty("audioAttach")]
        internal string AudioAttach { get; set; }
    }

    public class Environment
    {
        [JsonProperty("cameraExposure")]
        internal int CameraExposure { get; set; }

        [JsonProperty("clearColorRgba255")]
        internal ClearColorRgba255 ClearColorRgba255 { get; set; }

        [JsonProperty("enableSkybox")]
        internal bool EnableSkybox { get; set; }

        [JsonProperty("skybox")]
        internal Skybox Skybox { get; set; }

        [JsonProperty("ground")]
        internal Ground Ground { get; set; }

        [JsonProperty("lights")]
        internal Lights Lights { get; set; }
    }

    public class ClearColorRgba255
    {
        [JsonProperty("r")]
        internal int R { get; set; }

        [JsonProperty("g")]
        internal int G { get; set; }

        [JsonProperty("b")]
        internal int B { get; set; }

        [JsonProperty("a")]
        internal float A { get; set; }
    }

    public class Skybox
    {
        [JsonProperty("intensity")]
        internal int Intensity { get; set; }

        [JsonProperty("colorRgb255")]
        internal ColorRgb255 ColorRgb255 { get; set; }

        [JsonProperty("textureFile")]
        internal string TextureFile { get; set; }
    }

    public class ColorRgb255
    {
        [JsonProperty("r")]
        internal int R { get; set; }

        [JsonProperty("g")]
        internal int G { get; set; }

        [JsonProperty("b")]
        internal int B { get; set; }
    }

    public class Ground
    {
        [JsonProperty("colorRgb255")]
        internal ColorRgb255 ColorRgb255 { get; set; }
    }

    public class Lights
    {
        [JsonProperty("ambient")]
        internal Light Ambient { get; set; }

        [JsonProperty("directional")]
        internal Light Directional { get; set; }
    }

    public class Light
    {
        [JsonProperty("intensity")]
        internal int Intensity { get; set; }

        [JsonProperty("colorRgb255")]
        internal ClearColorRgba255 ColorRgb255 { get; set; }
    }

    public class Config
    {
        [JsonProperty("whiteLabel")]
        internal bool WhiteLabel { get; set; }

        [JsonProperty("being")]
        internal Being Being { get; set; }

        [JsonProperty("gdpr")]
        internal Gdpr Gdpr { get; set; }

        [JsonProperty("teaser")]
        internal string Teaser { get; set; }

        [JsonProperty("showSubtitles")]
        internal bool ShowSubtitles { get; set; }

        [JsonProperty("hideHistoryOnLoad")]
        internal bool HideHistoryOnLoad { get; set; }
    }

    public class Being
    {
        [JsonProperty("name")]
        public string Name { get; internal set; }
    }

    public class Gdpr
    {
        [JsonProperty("explicitConsentRequired")]
        internal bool ExplicitConsentRequired { get; set; }

        [JsonProperty("consentText")]
        internal string ConsentText { get; set; }

        [JsonProperty("consentButtonText")]
        internal string ConsentButtonText { get; set; }

        [JsonProperty("declineButtonText")]
        internal string DeclineButtonText { get; set; }

        [JsonProperty("declineConsentNotice")]
        internal string DeclineConsentNotice { get; set; }

        [JsonProperty("learnMoreUrl")]
        internal string LearnMoreUrl { get; set; }
    }
}