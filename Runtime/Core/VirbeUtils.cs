using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;


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
                oldConfig.Initialize();
                return oldConfig;
            }
            throw new Exception("[VIRBE] Could not parse json to config");
        }

        private enum SchemaVersion
        {
            v2,
            v3
        }
    }
}