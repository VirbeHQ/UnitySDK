using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using Virbe.Core.Data;

namespace Virbe.Core
{
    public static class VirbeUtils
    {
        public static IApiBeingConfig ParseConfig(string configJson)
        {
            var jsonObject = JObject.Parse(configJson);
            if (jsonObject.TryGetValue("schema", out JToken schemaToken))
            {
                if (!Enum.TryParse<SchemaVersion>(schemaToken.ToString(), true, out var result))
                {
                    throw new NotImplementedException($"[VIRBE] Schema version {result} is not implemented");
                }
                if (result == SchemaVersion.v3)
                {
                    var v3Config = JsonConvert.DeserializeObject<ApiBeingConfigv3>(configJson);
                    v3Config.Initialize();
                    return v3Config;
                }
            }
            else
            {
                var oldConfig = JsonConvert.DeserializeObject<ApiBeingConfig>(configJson);
                if(oldConfig?.location == null)
                {
                    throw new Exception($"[VIRBE] Could not parse json to config: json {configJson}");
                }
                oldConfig.Initialize();
                return oldConfig;
            }
            throw new Exception("[VIRBE] Could not parse json to config");
        }

        public static bool TryCreateUrlAddress(string url, out Uri address)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out address))
            {
                return (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps);
            }
            return false;
        }

        private enum SchemaVersion
        {
            v2,
            v3
        }
    }
}