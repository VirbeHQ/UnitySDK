using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Virbe.Core.Api;
using Virbe.Core.Logger;
using Virbe.Core.Speech;
using static Virbe.Core.CommunicationSystem;

namespace Virbe.Core
{
    internal sealed class EndlessSocketCommunicationHandler : ICommunicationHandler
    {
        private const string InitializationRequestName = "conversation-init";
        private const string SpeechRecognized = "speech-recognized";
        private const string SpeechInitialize = "speech-start";
        private const string SpeechEnd = "speech-end";
        private const string SpeechAudio = "speech-audio";
        private const string ConversationError = "conversation-error";
        private const string StateChanged = "state";

        bool ICommunicationHandler.Initialized => _initialized;
        private readonly RequestActionType _definedActions = RequestActionType.SendAudioStream;

        private bool _initialized;
        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(EndlessSocketCommunicationHandler));
        private SocketIOClient.SocketIO _socketClient;
        private ConcurrentQueue<byte[]> _speechBytesAwaitingSend = new ConcurrentQueue<byte[]>();
        private StringBuilder _currentSttResult = new StringBuilder();

        private CancellationTokenSource _endlessSocketTokenSource;
        private CancellationTokenSource _audioSocketSenderTokenSource;
        private ConversationData _data;
        private string _baseUrl;
        private ActionToken _actionToken;
        private VirbeUserSession _currentSession;

        internal EndlessSocketCommunicationHandler(string baseUrl, ConversationData data , ActionToken _actionToken)
        {
            _baseUrl = baseUrl;
            _data = data;
        }

        bool ICommunicationHandler.HasCapability(RequestActionType type)
        {
            return (_definedActions & type) == type;
        }

        Task ICommunicationHandler.Prepare(VirbeUserSession session)
        {
            _initialized = true;
            _currentSession = session;
            return ConnectToEndlessSocket();
        }

        internal void CloseSocket()
        {
            _audioSocketSenderTokenSource?.Cancel();
            DisposeSocketConnection();
        }

        private void SendSpeech(byte[] recordedAudioBytes)
        {
            if (_socketClient == null)
            {
                _logger.LogError($"[VIRBE] Socket not created, could not send speech chunk");
                return;
            }
            _speechBytesAwaitingSend.Enqueue(recordedAudioBytes);
        }

        private async UniTaskVoid SocketAudioSendLoop(CancellationToken cancelationToken)
        {
            while (!cancelationToken.IsCancellationRequested)
            {
                if (_socketClient.Connected && _speechBytesAwaitingSend.TryDequeue(out var chunk))
                {
                    await _socketClient.EmitAsync(SpeechAudio, chunk);
                }
                if (cancelationToken.IsCancellationRequested)
                {
                    return;
                }
                await Task.Delay(100);
            }
        }

        private void DisposeSocketConnection()
        {
            if (_socketClient?.Connected == true)
            {
                DisconnectFromSocket().Forget();
            }
            else
            {
                _speechBytesAwaitingSend.Clear();
                _endlessSocketTokenSource.Cancel();
            }
        }

        private Task ConnectToEndlessSocket()
        {
            _endlessSocketTokenSource?.Cancel();
            _endlessSocketTokenSource = new CancellationTokenSource();
            _socketClient = new SocketIOClient.SocketIO(_baseUrl);
            _socketClient.Options.EIO = SocketIO.Core.EngineIO.V4;
            _socketClient.Options.Path = _data.Path;

            _currentSttResult.Clear();

            _socketClient.On(SpeechRecognized, (response) =>
            {
              //  JArray jsonArray = JArray.Parse(response.ToString());
               // string result = (string)jsonArray[0]["text"];
                _logger.Log($"[{DateTime.Now}] Recognized text: {response}");
                //_currentSttResult.Append(result);
            });

            _socketClient.On(StateChanged, (response) =>
            {
                _logger.Log($"[{DateTime.Now}]Conversation state changed : {response}");
                var responseMessage = response.ToString();
                var state = JsonConvert.DeserializeObject<List<StateMessage>>(responseMessage);
                if(state.FirstOrDefault()?.IndicateInitialization == true)
                {
                    OnInitialized().Forget();
                }
            });

            _socketClient.OnConnected += (sender, args) =>
            {
                OnConnected().Forget();
            };

            _socketClient.OnDisconnected += async (sender, args) =>
            {
                _endlessSocketTokenSource?.Cancel();
                SendTextFromRresult();
                _logger.Log($"Disconnected from the stt socket {args}");
            };

            _socketClient.OnError += (sender, args) =>
            {
                _logger.Log($"Socket error: {args}");
            };
            
            _socketClient.On(ConversationError, (response) =>
            {
                _logger.Log($"[{DateTime.Now}]Conversation error occured : {response}");
            });

            _logger.Log($"Try connecting to socket.io endpoint: {_baseUrl}{_data.Path}");
            return _socketClient.ConnectAsync(_endlessSocketTokenSource.Token);
        }

        private async UniTaskVoid OnConnected()
        {
            _logger.Log($"Connected to the socket .");
            await InitializeConversation();
        }

        private async UniTaskVoid OnInitialized()
        {
            _logger.Log($"Conversation initialized - starting speech");

            await InitializeSpeech();

            _audioSocketSenderTokenSource?.Cancel();
            _audioSocketSenderTokenSource = new CancellationTokenSource();
            SocketAudioSendLoop(_audioSocketSenderTokenSource.Token).Forget();
        }
        private async Task InitializeConversation()
        {
            var conversationId = _currentSession.ConversationId ?? Guid.Empty.ToString();
            if (string.IsNullOrEmpty(_currentSession.UserId))
            {
                _logger.LogError($"{nameof(_currentSession.UserId)} must be UUID.");
                return;
            }
            var msg = new InitializeMessage() { endUserId = _currentSession.UserId };
            if (!string.IsNullOrEmpty(_currentSession.ConversationId))
            {
                msg.conversationId = _currentSession.ConversationId;
            }
            await _socketClient.EmitAsync(InitializationRequestName, msg);
           //_logger.Log(msg.ToString());
        }

        private async Task InitializeSpeech()
        {
            var msg = new SpeachInitMessage() {enableContinuousRecognition = true.ToString(), sendResultsToConversationEngine = true.ToString() };
            await _socketClient.EmitAsync(SpeechInitialize, msg);
            _logger.Log(msg.ToString());
        }

        private async Task StopConversation()
        {
            await _socketClient.EmitAsync(SpeechEnd);
        }

        private void SendTextFromRresult()
        {
            if (_currentSttResult.Length > 0)
            {
                var result = _currentSttResult.ToString();
                _logger.Log($"[{DateTime.Now}] Request send recognized text: {result}");
                _currentSttResult.Clear();
            }
        }

        private async UniTaskVoid DisconnectFromSocket()
        {
            _endlessSocketTokenSource?.Cancel();
            var tempSocketHandle = _socketClient;
            await StopConversation();
            await Task.Delay(250);
            await tempSocketHandle.DisconnectAsync();
            tempSocketHandle.Dispose();
            tempSocketHandle = null;
        }

        void IDisposable.Dispose()
        {
            _initialized = false;
            CloseSocket();
        }

        UniTask ICommunicationHandler.MakeAction(RequestActionType type, params object[] args)
        {
            if (type == RequestActionType.SendAudioStream)
            {
                SendSpeech(args[0] as byte[]);
            }
            return UniTask.CompletedTask;
        }

        private class InitializeMessage
        {
            public string endUserId { get; set; }
            public string conversationId { get; set; }

            public override string ToString()
            {
                return $"endUserId = {endUserId}, conversationId = {conversationId}";
            }
        }

        private class SpeachInitMessage
        {
            public string enableContinuousRecognition { get; set; }
            public string sendResultsToConversationEngine { get; set; }

            public override string ToString()
            {
                return $"enableContinuousRecognition = {enableContinuousRecognition}, sendResultsToConversationEngine = {sendResultsToConversationEngine}";
            }
        }
        // [{"previous":"socket","current":"initialized"}]

        private class StateMessage
        {
            public string previous { get; set; }
            public string current { get; set; }

            public bool IndicateInitialization => previous == "socket" && current == "initialized";
        }
    }
}