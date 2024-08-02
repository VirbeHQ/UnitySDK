using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Virbe.Core.Api;
using Virbe.Core.Logger;
using Virbe.Core.Speech;

namespace Virbe.Core
{
    internal sealed class STTSocketCommunicationHandler: ICommunicationHandler
    {
        internal event Action<string> RequestTextSend;

        bool ICommunicationHandler.Initialized => _initialized;
        private readonly RequestActionType _definedActions = RequestActionType.SendAudioStream;

        private bool _initialized;
        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(STTSocketCommunicationHandler));
        private SocketIOClient.SocketIO _socketSttClient;
        private ConcurrentQueue<byte[]> _speechBytesAwaitingSend = new ConcurrentQueue<byte[]>();
        private StringBuilder _currentSttResult = new StringBuilder();

        private CancellationTokenSource _sttSocketTokenSource;
        private CancellationTokenSource _audioSocketSenderTokenSource;
        private STTData _data;
        private Action _additionalDisposeAction;

        private string _baseUrl;

        internal STTSocketCommunicationHandler(string baseUrl, STTData data)
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
            return Task.CompletedTask;
        }

        internal void OpenSocket()
        {
            ConnectToSttSocket().Forget();
            _audioSocketSenderTokenSource?.Cancel();
            _audioSocketSenderTokenSource = new CancellationTokenSource();
            SocketAudioSendLoop(_audioSocketSenderTokenSource.Token).Forget();
        }

        internal void CloseSocket()
        {
            _audioSocketSenderTokenSource?.Cancel();
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
                    await _socketSttClient.EmitAsync("audio", AudioConverter.FromBytesToBase64(chunk));
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
            _currentSttResult.Clear();

            _logger.Log($"Try connecting to socket.io endpoint");

            _socketSttClient.On("upgrade", (response) =>
            {
                _logger.Log($"Upgraded transport: ${response}");
            });

            _socketSttClient.On("recognizing", (response) =>
            {
                JArray jsonArray = JArray.Parse(response.ToString());
                string result = (string)jsonArray[0]["text"];
                _logger.Log($"[{DateTime.Now}] Recognized text: {result}");
                _currentSttResult.Append(result);
            });

            _socketSttClient.On("connect_error", (response) =>
            {
                _logger.LogError($"Connection error : {response}");
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

            await _socketSttClient.ConnectAsync(_sttSocketTokenSource.Token);
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
            await Task.Delay(1500);
            SendTextFromRresult();
            await tempSocketHandle.DisconnectAsync();
            tempSocketHandle.Dispose();
            tempSocketHandle = null;
        }

        void IDisposable.Dispose()
        {
            _initialized = false;
            _additionalDisposeAction?.Invoke();
            CloseSocket();
        }

        UniTask ICommunicationHandler.MakeAction(RequestActionType type, params object[] args)
        {
            if(type == RequestActionType.SendAudioStream)
            {
                SendSpeech(args[0] as byte[]);
            }
            return UniTask.CompletedTask;
        }
    }
}