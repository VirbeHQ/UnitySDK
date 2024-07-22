using Newtonsoft.Json;


namespace Virbe.Core
{
    public static class VirbeUtils
    {
        public static IApiBeingConfig ParseConfig(string configJson)
        {
            var being = JsonConvert.DeserializeObject<ApiBeingConfig>(configJson);
            return being;
        }
    }
}