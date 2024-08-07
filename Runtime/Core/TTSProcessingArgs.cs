using Plugins.Virbe.Core.Api;
using System;

namespace Virbe.Core
{
    public struct TTSProcessingArgs
    {
        public readonly string Text;
        public readonly Guid ID;
        public readonly Action<RoomDto.BeingVoiceData> Callback;

        public TTSProcessingArgs (string text, Guid id,Action<RoomDto.BeingVoiceData> callback)
        {
            Text = text;
            Callback = callback;
            ID = id;
        }
    }
}