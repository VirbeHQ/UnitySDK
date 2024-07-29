using Plugins.Virbe.Core.Api;

namespace Virbe.Core
{
    public interface IApiBeingConfig
    {
        string BaseUrl { get; }
        EngineType EngineType { get; }
        RoomData RoomData { get; }
        bool HasRoom { get; }

        //tts
        int AudioChannels { get; }
        int AudioFrequency { get; }
        int AudioSampleBits { get; }

        //stt
        SttConnectionProtocol SttProtocol { get; }
        string SttPath { get; }

        RoomApiService CreateRoomObject(string endUserId);
    }
}