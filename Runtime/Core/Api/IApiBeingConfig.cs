using Plugins.Virbe.Core.Api;

namespace Virbe.Core
{
    public interface IApiBeingConfig
    {
        string BaseUrl { get; }
        string HostDomain { get; }
        string RoomApiAccessKey { get; }
        string RoomUrl { get; }
        bool RoomEnabled { get; }
        bool HasRoom { get; }

        //tts
        int AudioChannels { get; }
        int AudioFrequency { get; }
        int AudioSampleBits { get; }

        //stt
        SttConnectionProtocol SttProtocol { get; }
        string SttPath { get; }

        bool HasValidApiAccessKey();
        bool HasValidHostDomain();

        RoomApiService CreateRoom(string endUserId);
    }
}