using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Plugins.Virbe.Core.Api;
using Virbe.Core.Actions;
using Virbe.Core.Api;
using Virbe.Core.Logger;

namespace Virbe.Core
{
    internal sealed class TTSCommunicationHandler : ICommunicationHandler
    {
        private class TTSResponseModel
        {
            public List<BeingAction.Mark> marks;
            public byte[] speech;
        }
        bool ICommunicationHandler.Initialized => _initialized;
        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(TTSCommunicationHandler));

        private readonly TTSData _data;
        private readonly string _endpoint;
        private RequestActionType _definedActions = RequestActionType.ProcessTTS;
        private bool _initialized;

        internal TTSCommunicationHandler(string baseUrl, TTSData data)
        {
            _data = data;
            _endpoint = baseUrl;
        }

        void IDisposable.Dispose()
        {
            _initialized = false;
        }

        bool ICommunicationHandler.HasCapability(RequestActionType type) => (_definedActions & type) == type;

        async UniTask ICommunicationHandler.MakeAction(RequestActionType type, params object[] args)
        {
            if (type == RequestActionType.ProcessTTS)
            {
                try
                {
                    var textToProcess = args[0] as string;
                    var resultData = await ProcessText(textToProcess);
                    var voiceData = new RoomDto.BeingVoiceData()
                    {
                        marks = resultData.marks,
                        data = resultData.speech,
                    };
                    var action = args[1] as Action<RoomDto.BeingVoiceData>;
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

        private async Task<TTSResponseModel> ProcessText(string text)
        {
            return await Request<TTSResponseModel>($"{_endpoint}{_data.Path}", HttpMethod.Post, new Dictionary<string, string>(), true, $"{{ \"text\" : \"{text}\" }}");
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

    }
}