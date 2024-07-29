using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
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
        private VirbeUserSession _currentUserSession;
        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(STTSocketCommunicationHandler));
        private SocketIOClient.SocketIO _socketSttClient;
        private ConcurrentQueue<byte[]> _speechBytesAwaitingSend = new ConcurrentQueue<byte[]>();
        private StringBuilder _currentSttResult = new StringBuilder();

        private CancellationTokenSource _sttSocketTokenSource;
        private CancellationTokenSource _audioSocketSenderTokenSource;
        private IApiBeingConfig _apiBeingConfig;
        private VirbeBeing _being;

        internal STTSocketCommunicationHandler(VirbeBeing being)
        {
            _being = being;
            _apiBeingConfig = being.ApiBeingConfig;
            _being.UserStartSpeaking += OpenSocket;
            _being.UserStopSpeaking += CloseSocket;
        }

        bool ICommunicationHandler.HasCapability(RequestActionType type)
        {
            return (_definedActions & type) == type;
        }

         Task ICommunicationHandler.Prepare(VirbeUserSession session)
        {
            _currentUserSession = session;
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

        private void SendSpeech(byte[] recordedAudioBytes)
        {
            if (!_apiBeingConfig.RoomData.Enabled)
            {
                return;
            }
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
            _socketSttClient = new SocketIOClient.SocketIO(_apiBeingConfig.BaseUrl);
            _socketSttClient.Options.EIO = SocketIO.Core.EngineIO.V4;
            _socketSttClient.Options.Path = _apiBeingConfig.SttPath;
            _currentSttResult.Clear();

            _logger.Log($"Try connecting to socket.io endpoint");

            _socketSttClient.On("upgrade", (response) =>
            {
                _logger.Log($"Upgraded transport: ${response}");
            });

            _socketSttClient.On("recognizing", (response) =>
            {
                //TODO: extract string from message
                _logger.Log($"Recognized text: {response}");
                _currentSttResult.Append(response.ToString());
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
                RequestTextSend?.Invoke(_currentSttResult.ToString());
                _currentSttResult.Clear();
            }
        }

        private async UniTaskVoid DisconnectFromSttSocket()
        {
            _sttSocketTokenSource?.Cancel();
            var tempSocketHandle = _socketSttClient;
            await Task.Delay(1000);
            SendTextFromRresult();
            await tempSocketHandle.DisconnectAsync();
            tempSocketHandle.Dispose();
            tempSocketHandle = null;
        }

        void IDisposable.Dispose()
        {
            _initialized = false;
            CloseSocket();
            _currentUserSession = null;
            RequestTextSend = null;
            _being.UserStartSpeaking -= OpenSocket;
            _being.UserStopSpeaking -= CloseSocket;
        }

        Task ICommunicationHandler.MakeAction(RequestActionType type, params object[] args)
        {
            throw new NotImplementedException();
        }
    }
}