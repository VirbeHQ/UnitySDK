using System;
using JetBrains.Annotations;
using UnityEngine;

namespace Virbe.Core
{
    [Serializable]
    public class ApiBeingConfig
    {
        [SerializeField] public RoomConfig room;
        [SerializeField] public LocationConfig location;
        [SerializeField] public SttConfig sttConfig;
        [SerializeField] public TtsConfig ttsConfig;
        [SerializeField] public HostConfig host;

        public ApiBeingConfig()
        {
            sttConfig = new SttConfig();
            ttsConfig = new TtsConfig();
            host = new HostConfig();
        }
        
        private Uri RoomUri => new Uri(room?.roomUrl);
        public string HostDomain => !string.IsNullOrEmpty(room?.roomUrl) ? RoomUri.GetLeftPart(UriPartial.Authority) : null;
        public string RoomApiAccessKey => room?.roomApiAccessKey;

        public bool HasValidHostDomain()
        {
            return !string.IsNullOrEmpty(HostDomain);
        }

        public bool HasValidApiAccessKey()
        {
            return !string.IsNullOrEmpty(RoomApiAccessKey);
        }
        
        [Serializable]
        public class PresenterConfig
        {
            [SerializeField] protected internal bool enabled = false;
            [SerializeField] protected internal string assetsPath;
            [SerializeField] protected internal string welcomeNode;
            [SerializeField] protected internal string defaultNode;
        }

        [Serializable]
        public class ChatConfig
        {
            [SerializeField] protected internal bool enabled = false;
            [SerializeField] protected internal string origin;
            [SerializeField] protected internal string chatUrl;
            [SerializeField] protected internal string chatApiAccessKey;
        }
        
        [Serializable]
        public class RoomConfig
        {
            [SerializeField] protected internal bool enabled = false;
            [SerializeField] protected internal string roomUrl;
            [SerializeField] protected internal string roomApiAccessKey;
        }
        
        [Serializable]
        public class LocationConfig
        {
            [SerializeField] protected internal string id;
            [SerializeField] protected internal string name;
            [SerializeField] protected internal string channel;
        }

        [Serializable]
        public class SttConfig
        {
            [SerializeField] protected internal string engine = "default";
            [SerializeField] public int recordingFrequency = 16000;
            [SerializeField] public int recordingChannels = 1;
        }

        [Serializable]
        public class TtsConfig
        {
            [SerializeField] protected internal string engine = "default";
            [SerializeField] public int audioFrequency = 16000;
            [SerializeField] public int audioSampleBits = 16;
            [SerializeField] public int audioChannels = 1;
            [SerializeField] protected internal string audioType = "pcm";
        }

        [Serializable]
        public class HostConfig
        {
            [SerializeField] public string type = "rpm";
            [SerializeField] public Character character;
        }

        [Serializable]
        public class Character
        {
            [SerializeField] public string mainFile;
        }
    }
}