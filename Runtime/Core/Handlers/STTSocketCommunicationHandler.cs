using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Virbe.Core.Data;

namespace Virbe.Core.Handlers
{
    internal sealed class STTSocketCommunicationHandler: ICommunicationHandler
    {
        internal event Action<string> RequestTextSend;

        bool ICommunicationHandler.Initialized => _initialized;
        private const string SpeechAudio = "speech-audio";
        private const string SpeechRecognized = "speech-recognized";

        public readonly RequestActionType DefinedActions = RequestActionType.SendAudioStream;

        private bool _initialized;
        private readonly ILogger _logger;
        private SocketIOClient.SocketIO _socketSttClient;
        private ConcurrentQueue<byte[]> _speechBytesAwaitingSend = new ConcurrentQueue<byte[]>();
        private StringBuilder _currentSttResult = new StringBuilder();

        private CancellationTokenSource _sttSocketTokenSource;
        private CancellationTokenSource _audioSocketSenderTokenSource;
        private bool _requestedSocketStateOpen;

        private STTData _data;
        private Action _additionalDisposeAction;
        private Action<Dictionary<string, string>> _updateHeader;

        private string _baseUrl;

        internal STTSocketCommunicationHandler(string baseUrl, STTData data, ILogger logger = null)
        {
            _baseUrl = baseUrl;
            _data = data;
            _logger = logger;
        }

        bool ICommunicationHandler.HasCapability(RequestActionType type)
        {
            return (DefinedActions & type) == type;
        }

        Task ICommunicationHandler.Prepare(VirbeUserSession session)
        {
            _initialized = true;
            return Task.CompletedTask;
        }

        Task ICommunicationHandler.MakeAction(RequestActionType type, params object[] args)
        {
            if (type == RequestActionType.SendAudioStream)
            {
                SendSpeech(args[0] as byte[]);
            }
            return Task.CompletedTask;
        }

        internal void SetHeaderUpdate(Action<Dictionary<string,string>> updateHeaderAction)
        {
            _updateHeader = updateHeaderAction;
        }

        internal void OpenSocket()
        {
            _requestedSocketStateOpen = true;
            ConnectToSttSocket().Forget();
            _audioSocketSenderTokenSource?.Cancel();
            _audioSocketSenderTokenSource = new CancellationTokenSource();
            SocketAudioSendLoop(_audioSocketSenderTokenSource.Token).Forget();
        }

        internal void CloseSocket()
        {
            _requestedSocketStateOpen = false;
            DisposeSocketConnection();
        }

        internal void SetAdditionalDisposeAction(Action callback) => _additionalDisposeAction = callback;

        private void SendSpeech(byte[] recordedAudioBytes)
        {
            if (_socketSttClient == null)
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
                if (_socketSttClient.Connected && _speechBytesAwaitingSend.TryDequeue(out var chunk))
                {
                    await _socketSttClient.EmitAsync(SpeechAudio, chunk);
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
            if (_socketSttClient?.Connected == true)
            {
                DisconnectFromSttSocket().Forget();
            }
            else
            {
                _speechBytesAwaitingSend.Clear();
                _sttSocketTokenSource.Cancel();
            }
        }

        private async UniTaskVoid ConnectToSttSocket()
        {
            _sttSocketTokenSource?.Cancel();
            _sttSocketTokenSource = new CancellationTokenSource();
            _socketSttClient = new SocketIOClient.SocketIO(_baseUrl);
            _socketSttClient.Options.EIO = SocketIO.Core.EngineIO.V4;
            _socketSttClient.Options.Path = _data.Path;
            _socketSttClient.Options.Transport = SocketIOClient.Transport.TransportProtocol.WebSocket;

            //if(_socketSttClient.Options.ExtraHeaders == null)
            //{
            //    _socketSttClient.Options.ExtraHeaders = new Dictionary<string, string>();
            //}
            //_updateHeader?.Invoke(_socketSttClient.Options.ExtraHeaders);
            _currentSttResult.Clear();

            _logger.Log($"Try connecting to STT socket endpoint");

            _socketSttClient.On("upgrade", (response) =>
            {
                _logger.Log($"Upgraded transport: ${response}");
            });

            _socketSttClient.On(SpeechRecognized, (response) =>
            {
                _logger.Log($"[{DateTime.Now}] Recognized text: {response}");
            });


            _socketSttClient.OnConnected += (sender, args) =>
            {
                _logger.Log($"Connected to the stt socket .");
            };

            _socketSttClient.OnError += (sender, args) =>
            {
                _logger.Log($"Socket error: {args}");
            };

            _socketSttClient.OnDisconnected += async (sender, args) =>
            {
                _sttSocketTokenSource?.Cancel();
                SendTextFromRresult();
                _logger.Log($"Disconnected from the stt socket {args}");
            };
            if (_requestedSocketStateOpen)
            {
                await _socketSttClient.ConnectAsync(_sttSocketTokenSource.Token);
            }
        }

        private void SendTextFromRresult()
        {
            if (_currentSttResult.Length > 0)
            {
                var result = _currentSttResult.ToString();
                _logger.Log($"[{DateTime.Now}] Request send recognized text: {result}");
                RequestTextSend?.Invoke(result);
                _currentSttResult.Clear();
            }
        }

        private async UniTaskVoid DisconnectFromSttSocket()
        {
            _sttSocketTokenSource?.Cancel();
            var tempSocketHandle = _socketSttClient;
            await Task.Delay(3500);
            SendTextFromRresult();
            //in case user callOpenSocket during closing action
            if (!_requestedSocketStateOpen)
            {
                _audioSocketSenderTokenSource?.Cancel();
                await tempSocketHandle.DisconnectAsync();
                tempSocketHandle.Dispose();
            }
            _socketSttClient = null;
        }

        void IDisposable.Dispose()
        {
            _initialized = false;
            _additionalDisposeAction?.Invoke();
            CloseSocket();
        }

        Task ICommunicationHandler.ClearProcessingQueue()
        {
            _speechBytesAwaitingSend.Clear();
            _currentSttResult.Clear();
            return Task.CompletedTask;
        }
    }
}