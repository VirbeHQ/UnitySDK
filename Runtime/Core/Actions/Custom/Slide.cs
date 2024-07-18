using Newtonsoft.Json.Linq;

namespace Virbe.Core.Custom
{
    [System.Serializable]
    public struct Slide
    {
        public readonly string ImageUrl;

        public Slide(JObject productDict)
        {
            ImageUrl = productDict.Value<string>("imageUrl") ??  "";
        }
    }
}