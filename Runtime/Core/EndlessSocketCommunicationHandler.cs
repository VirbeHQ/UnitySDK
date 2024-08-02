using System;
using System.Collections.Concurrent;
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
        bool ICommunicationHandler.Initialized => _initialized;
        private readonly RequestActionType _definedActions = RequestActionType.SendAudioStream;

        private bool _initialized;
        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(STTSocketCommunicationHandler));
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
                    await _socketClient.EmitAsync("conversation-speech", AudioConverter.FromBytesToBase64(chunk));
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

            _socketClient.On("recognizing", (response) =>
            {
              //  JArray jsonArray = JArray.Parse(response.ToString());
               // string result = (string)jsonArray[0]["text"];
                _logger.Log($"[{DateTime.Now}] Recognized text: {response}");
                //_currentSttResult.Append(result);
            });

            _socketClient.On("state", (response) =>
            {
                _logger.Log($"[{DateTime.Now}]Conversation state changed : {response}");
            });

            _socketClient.OnConnected += (sender, args) =>
            {
                OnConnected().Forget();
            };

            _socketClient.OnError += (sender, args) =>
            {
                _logger.Log($"Socket error: {args}");
            };

            _socketClient.OnDisconnected += async (sender, args) =>
            {
                _endlessSocketTokenSource?.Cancel();
                SendTextFromRresult();
                _logger.Log($"Disconnected from the stt socket {args}");
            };

            _logger.Log($"Try connecting to socket.io endpoint: {_baseUrl}{_data.Path}");
            return _socketClient.ConnectAsync(_endlessSocketTokenSource.Token);
        }

        private async UniTaskVoid OnConnected()
        {
            _logger.Log($"Connected to the socket .");
            await InitializeConversation();

            _audioSocketSenderTokenSource?.Cancel();
            _audioSocketSenderTokenSource = new CancellationTokenSource();
            SocketAudioSendLoop(_audioSocketSenderTokenSource.Token).Forget();
        }

        private async Task InitializeConversation()
        {
            var msg = new InitializeMessage() { endUserId = _currentSession.UserId, conversationId = _currentSession.ConversationId };
            var json = JsonConvert.SerializeObject(msg);
            _logger.Log(json);
            //TODO: fix [WebSocket⬆] 42["conversation-init","{\"endUserId\":\"21a7c0a2-9b4c-4934-ad72-e203b7814930\",\"conversationId\":null}"]
            //[WebSocket⬇] 42["conversation-error",{ "code":"handler-wrong-state","current":"socket","expected":"initialized"}]
            await _socketClient.EmitAsync("conversation-init", json);
        }

        private async UniTaskVoid SendMessage()
        {
            //conversation-message
            //await _socketClient.EmitAsync("conversation-message", JsonConvert.SerializeObject(msg));
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
            await Task.Delay(1500);
            SendTextFromRresult();
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
        }
    }
}