using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Virbe.Core.Data;
using Virbe.Core.Logger;

namespace Virbe.Core.Handlers
{
    internal sealed class TTSCommunicationHandler : ICommunicationHandler
    {
        bool ICommunicationHandler.Initialized => _initialized;
        private readonly Virbe.Core.ILogger _logger;

        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>();
        private Action<Dictionary<string, string>> _updateHeader;

        private readonly TTSData _data;
        private readonly Uri _endpoint;
        public readonly RequestActionType DefinedActions = RequestActionType.ProcessTTS;
        private bool _initialized;

        internal TTSCommunicationHandler(string baseUrl, TTSData data, string locationId, Virbe.Core.ILogger logger = null)
        {
            _logger = logger;
            _data = data;
            _endpoint = new Uri(baseUrl);
            //_headers.Add("origin", baseUrl);
            //_headers.Add("referer", baseUrl);
        }

        internal void SetHeaderUpdate(Action<Dictionary<string, string>> updateHeaderAction)
        {
            _updateHeader = updateHeaderAction;
        }

        void IDisposable.Dispose()
        {
            _initialized = false;
        }

        bool ICommunicationHandler.HasCapability(RequestActionType type) => (DefinedActions & type) == type;

        async Task ICommunicationHandler.MakeAction(RequestActionType type, params object[] args)
        {
            if (type == RequestActionType.ProcessTTS)
            {
                try
                {
                    var textToProcess = args[0] as string;
                    _logger.Log($"TTS processing : \"{textToProcess}\"");

                    var resultData = await ProcessText(textToProcess);
                    var voiceData = new VoiceData()
                    {
                        Marks = resultData.marks,
                        Data = resultData.speech,
                        AudioParameters = resultData.audioParameters
                    };
                    var action = args[1] as Action<VoiceData>;
                    _logger.Log($"TTS processed : \"{textToProcess}\"  and propagated response.");
                    action?.Invoke(voiceData);
                }
                catch(Exception e)
                {
                    _logger.LogError($"Error during TTS processing: {e.Message}");
                }
            }
        }

        Task ICommunicationHandler.Prepare(VirbeUserSession session)
        {
            _initialized = true;
            return Task.CompletedTask;
        }

        private async Task<TTSResponseModel> ProcessText(string text, string language = null)
        {
            var msg = new RequestMessage() {text = text, language = language, personaId = null};
            var json = JsonConvert.SerializeObject(msg);
            _updateHeader?.Invoke(_headers);
            Uri requestUri = new Uri(_endpoint, _data.Path);
            return await Request<TTSResponseModel>(requestUri.AbsoluteUri, HttpMethod.Post, _headers, true, json);
        }

        private async Task<T> Request<T>(string endpoint, HttpMethod method, Dictionary<string, string> headers, bool ensureSuccess,
          string body)
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders
                .Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json")); //

            var request = new HttpRequestMessage(method, endpoint);
            httpClient.Timeout = new TimeSpan(0, 0, 15);

            if (body != null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            foreach (var header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }

            try
            {
                var response = await httpClient.SendAsync(request);

                if (ensureSuccess)
                {
                    response.EnsureSuccessStatusCode();
                }
                else if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    return default(T);
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var responseData = JsonConvert.DeserializeObject<T>(responseJson);

                return responseData;
            }
            catch (Exception ex)
            {
                if (ensureSuccess)
                {
                    throw;
                }
                return default(T);
            }
        }

        private class RequestMessage
        {
            public string text { get; set; }
            public string language { get; set; }
            public string personaId { get; set; }
        }
        private class TTSResponseModel
        {
            public List<Mark> marks;
            public byte[] speech;
            public string text;
            public AudioParameters audioParameters;
        }
    }
}