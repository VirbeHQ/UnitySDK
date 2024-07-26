using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Plugins.Virbe.Core.Api;
using Virbe.Core.Actions;
using Virbe.Core.Api;
using Virbe.Core.Logger;
using Virbe.Core.Speech;


namespace Virbe.Core
{
    internal sealed class SocketCommunicationHandler: ICommunicationHandler
    {
        bool ICommunicationHandler.Initialized => _initialized;
        bool ICommunicationHandler.AudioStreamingEnabled => true;
        event Action<UserAction> ICommunicationHandler.UserActionFired
        {
            add { UserActionFired += value; }
            remove { UserActionFired -= value; }
        }

        event Action<BeingAction> ICommunicationHandler.BeingActionFired
        {
            add { BeingActionFired += value; }
            remove { BeingActionFired -= value; }
        }

        private bool _initialized;
        private VirbeUserSession CurrentUserSession;
        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(SocketCommunicationHandler));
        private SocketIOClient.SocketIO _socketSttClient;
        private ConcurrentQueue<byte[]> _speechBytesAwaitingSend = new ConcurrentQueue<byte[]>();
        private StringBuilder _currentSttResult = new StringBuilder();
        private RoomApiService _roomApiService;

        private CancellationTokenSource _sttSocketTokenSource;
        private CancellationTokenSource _audioSocketSenderTokenSource;
        private IApiBeingConfig _apiBeingConfig;
        private VirbeBeing _being;
        private event Action<BeingAction> BeingActionFired;
        private event Action<UserAction> UserActionFired;

        internal SocketCommunicationHandler(VirbeBeing being)
        {
            _being = being;
            _apiBeingConfig = being.ApiBeingConfig;
            _being.UserStartSpeaking += OpenSocket;
            _being.UserStopSpeaking += CloseSocket;
        }

        async Task ICommunicationHandler.Prepare(string userId, string conversationId)
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

            _initialized = true;
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

        Task ICommunicationHandler.SendSpeech(byte[] recordedAudioBytes)
        {
            if (!_apiBeingConfig.RoomEnabled)
            {
                return Task.CompletedTask;
            }
            if (_socketSttClient == null)
            {
                _logger.LogError($"[VIRBE] Socket not created, could not send speech chunk");
                return Task.CompletedTask;
            }
            _speechBytesAwaitingSend.Enqueue(recordedAudioBytes);
            return Task.CompletedTask;
        }

        Task ICommunicationHandler.SendNamedAction(string name, string value) => _roomApiService.SendNamedAction(name, value);

        Task ICommunicationHandler.SendText(string text) => _roomApiService.SendText(text);

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

        void IDisposable.Dispose()
        {
            _initialized = false;
            CloseSocket();
            CurrentUserSession = null;
            _roomApiService = null;
            _being.UserStartSpeaking -= OpenSocket;
            _being.UserStopSpeaking -= CloseSocket;
        }
    }
}