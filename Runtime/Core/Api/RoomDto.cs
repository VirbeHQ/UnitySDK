using JetBrains.Annotations;
using Virbe.Core.Custom;
using Virbe.Core.Actions;
using Virbe.Core.Speech;

namespace Plugins.Virbe.Core.Api
{
    using System;
    using System.Collections.Generic;

    public static class RoomDto
    {
        [Serializable]
        public class Room
        {
            public string id;
            public string locationId;
            public string clientIp;
            public string origin;
            public string userAgent;
            public string referer;
            public string createdAt;
            public string modifiedAt;
        }

        [Serializable]
        public class RoomsApiResponse
        {
            public int count;
            public List<Room> results;
        }

        [Serializable]
        public class RoomMessageActionSpeech
        {
            public string speech;

            public RoomMessageActionSpeech(byte[] bytes)
            {
                speech = AudioConverter.FromBytesToBase64(bytes);
            }
        }

        [Serializable]
        public class RoomMessageActionText
        {
            public string text;
            public string language;

            public RoomMessageActionText(string text, string language = null)
            {
                this.text = text;
                this.language = language;
            }
        }

        [Serializable]
        public class RoomMessageAction
        {
            public RoomMessageActionSpeech speech;
            public RoomMessageActionText text;
            public RoomMessageUiAction uiAction;

            public object roomStore; // Replace 'object' with the actual type if known
            public object endUserStore; // Replace 'object' with the actual type if known
            public RoomMessageNamedAction namedAction; // Replace 'object' with the actual type if known
        }

        [Serializable]
        public class RoomMessageUiAction
        {
            public string name;
            public RoomMessageUiActionValue value;
        }

        [Serializable]
        public class RoomMessageNamedAction
        {
            public string name;
            [CanBeNull] public string value;

            public RoomMessageNamedAction(string name, string value)
            {
                this.name = name;
                this.value = value;
            }
        }

        [Serializable]
        public class RoomMessageUiActionValue
        {
            public List<Button> buttons;
            public List<Card> cards;
        }

        [Serializable]
        public class RoomMessagePost
        {
            public string endUserId;
            public RoomMessageAction action;
        }

        [Serializable]
        public class RoomMessage
        {
            public string id;
            public string roomId;
            public string participantId;
            public string participantType;
            public RoomMessageAction action;
            public string replyTo;
            public string instant;
        }

        [Serializable]
        public class BeingVoiceData
        {
            public List<BeingAction.Mark> marks;
            public byte[] data;
        }

        [Serializable]
        public class RoomMessagesApiResponse
        {
            public int count;
            public List<RoomMessage> results;
        }
    }
}