using System;
using Newtonsoft.Json.Linq;

namespace Virbe.Core.Custom
{
    [Serializable]
    public struct Input
    {
        public readonly string StoreKey;
        public readonly string InputType;
        public readonly string InputLabel;
        public readonly InputButton SubmitButton;
        public readonly InputButton CancelButton;


        public Input(JObject inputDict) : this()
        {
            this.StoreKey = inputDict.Value<string>("storeKey");
            this.InputType = inputDict.Value<string>("inputType");
            this.InputLabel = inputDict.Value<string>("inputLabel");
            this.SubmitButton = new InputButton(inputDict.Value<JObject>("submitButton"));
            this.CancelButton = new InputButton(inputDict.Value<JObject>("cancelButton"));
        }
    }

    [Serializable]
    public struct InputButton
    {
        public readonly string Title;
        public readonly string PayloadType;
        public readonly string Payload;

        public InputButton(JObject buttonDict) : this()
        {
            Title = buttonDict.Value<string>("title") ?? "";
            PayloadType = buttonDict.Value<string>("payloadType") ?? "";
            Payload = buttonDict.Value<string>("payload") ?? "";
        }
    }
}