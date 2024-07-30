using Plugins.Virbe.Core.Api;

namespace Virbe.Core
{
    public interface IApiBeingConfig
    {
        string BaseUrl { get; }
        EngineType EngineType { get; }
        bool HasRoom { get; }
        RoomData RoomData { get; }
        TTSData TTSData { get; }

        //stt
        SttConnectionProtocol SttProtocol { get; }
        string SttPath { get; }

        RoomApiService CreateRoomObject(string endUserId);
    }
}