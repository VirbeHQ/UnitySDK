using Newtonsoft.Json;

namespace Virbe.Core.Custom
{
    [System.Serializable]
    public struct Button
    {
        [JsonProperty("label")]
        public string Title { get; set; }
        [JsonProperty("payloadType")]
        public string PayloadType { get; set; }
        [JsonProperty("payload")]
        public string Payload { get; set; }
        [JsonProperty("callbackUrl")]
        public string CallbackURL { get; set; }
    }
}