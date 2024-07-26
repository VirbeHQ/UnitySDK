using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Plugins.Virbe.Core.Api;
using Virbe.Core.Api;
using Virbe.Core.Logger;
using Virbe.Core.Speech;


namespace Virbe.Core
{
    public class SocketCommunicationHandler:IDisposable
    {
        internal bool Initialized { get; private set; }
        internal VirbeUserSession CurrentUserSession { get; private set; }

        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(SocketCommunicationHandler));
        private SocketIOClient.SocketIO _socketSttClient;
        private ConcurrentQueue<byte[]> _speechBytesAwaitingSend = new ConcurrentQueue<byte[]>();
        private StringBuilder _currentSttResult = new StringBuilder();
        private RoomApiService _roomApiService;

        private CancellationTokenSource _sttSocketTokenSource;
        private CancellationTokenSource _audioSocketSenderTokenSource;
        private IApiBeingConfig _apiBeingConfig;

        public SocketCommunicationHandler(IApiBeingConfig apiBeingConfig)
        {
            _apiBeingConfig = apiBeingConfig;
        }

        internal async Task Prepare(string userId = null, string conversationId = null)
        {
            CurrentUserSession = new VirbeUserSession(userId, conversationId);
            if (string.IsNullOrEmpty(conversationId))
            {
                _roomApiService = _apiBeingConfig.CreateRoom(CurrentUserSession.EndUserId);
                var createRoomTask = _roomApiService.CreateRoom();
                await createRoomTask;

                if (createRoomTask.IsFaulted)
                {
                    _logger.LogError("Failed to create room: " + createRoomTask.Exception?.Message);
                }
                else if (createRoomTask.IsCompleted)
                {
                    var createdRoom = createRoomTask.Result;
                    _logger.Log("Room created successfully. Room ID: " + createdRoom.id);
                    CurrentUserSession.UpdateSession(CurrentUserSession.EndUserId, createdRoom.id);
                }
            }

            Initialized = true;
        }

        internal async UniTaskVoid StartSending()
        {
            ConnectToSttSocket().Forget();
            _audioSocketSenderTokenSource?.Cancel();
            _audioSocketSenderTokenSource = new CancellationTokenSource();
            await SocketAudioSendLoop(_audioSocketSenderTokenSource.Token);
        }

        internal void StopSending()
        {
            _audioSocketSenderTokenSource?.Cancel();
            DisposeSocketConnection();
        }

        internal void EnqueueChunk(byte[] recordedAudioBytes)
        {
            if (_apiBeingConfig.RoomEnabled)
            {
                if (_socketSttClient == null)
                {
                    _logger.LogError($"[VIRBE] Socket not created, could not send speech chunk");
                    return;
                }
                _speechBytesAwaitingSend.Enqueue(recordedAudioBytes);
            }
        }

        private async Task SocketAudioSendLoop(CancellationToken cancelationToken)
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
                _currentSttResult.Append(response);
                _logger.Log($"Recognized text: {response}");
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
                SendTextFromRresult().Forget();
                _logger.Log($"Disconnected from the stt socket {args}");
            };

            await _socketSttClient.ConnectAsync(_sttSocketTokenSource.Token);
        }

        private async UniTaskVoid SendTextFromRresult()
        {
            if (_currentSttResult.Length > 0)
            {
                await _roomApiService.SendText(_currentSttResult.ToString());
                _currentSttResult.Clear();
            }
        }

        private async UniTaskVoid DisconnectFromSttSocket()
        {
            _sttSocketTokenSource?.Cancel();
            var tempSocketHandle = _socketSttClient;
            await Task.Delay(1000);
            SendTextFromRresult().Forget();
            _currentSttResult.Clear();
            await tempSocketHandle.DisconnectAsync();
            tempSocketHandle.Dispose();
            tempSocketHandle = null;
        }

        public void Dispose()
        {
            Initialized = false;
            StopSending();
            CurrentUserSession = null;
            _roomApiService = null;
        }
    }
}