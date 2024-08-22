using Plugins.Virbe.Core.Api;
using System;

namespace Virbe.Core
{
    public struct TTSProcessingArgs
    {
        public readonly string Text;
        public readonly string Lang;
        public readonly string Voice;
        public readonly Guid ID;
        public readonly Action<RoomDto.BeingVoiceData> Callback;

        public TTSProcessingArgs (string text, Guid id, string lang = null, string voice = null, Action<RoomDto.BeingVoiceData> callback = null)
        {
            Text = text;
            Callback = callback;
            ID = id;
            Lang = lang;
            Voice = voice;
        }
    }
}