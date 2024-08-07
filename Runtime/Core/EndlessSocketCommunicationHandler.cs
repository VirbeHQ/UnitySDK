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
using Plugins.Virbe.Core.Api;
using Virbe.Core.Actions;
using Virbe.Core.Api;
using Virbe.Core.Logger;
using Virbe.Core.Speech;
using static Virbe.Core.CommunicationSystem;

namespace Virbe.Core
{
    internal sealed class EndlessSocketCommunicationHandler : ICommunicationHandler
    {
        public event Action<string, Action<RoomDto.BeingVoiceData>> RequestTTSProcessing;

        private const string ConversationInitialize = "conversation-init";
        private const string ConversationMessage = "conversation-message";
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
        private List<SupportedPayload> _supportedPayloads;

        internal EndlessSocketCommunicationHandler(string baseUrl, ConversationData data , ActionToken actionToken)
        {
            _baseUrl = baseUrl;
            _data = data;
            _supportedPayloads = data.SupportedPayloads;
            _actionToken = actionToken;
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

            _socketClient.On(ConversationMessage, (response) => HandleConversationMessage(response.ToString()));

            _socketClient.On(SpeechRecognized, (response) => _logger.Log($"[{DateTime.Now}] Recognized text: {response}"));

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

            _socketClient.OnConnected += (sender, args) => OnConnected().Forget(); 

            _socketClient.OnDisconnected += (sender, args) =>
            {
                _endlessSocketTokenSource?.Cancel();
                _logger.Log($"Disconnected from the stt socket {args}");
            };

            _socketClient.OnError += (sender, args) => _logger.Log($"Socket error: {args}");
            
            _socketClient.On(ConversationError, (response) => _logger.Log($"[{DateTime.Now}]Conversation error occured : {response}"));

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
            await _socketClient.EmitAsync(ConversationInitialize, msg);
           //_logger.Log(msg.ToString());
        }

        private async Task InitializeSpeech()
        {
            var msg = new SpeachInitMessage() {enableContinuousRecognition = "true", sendResultsToConversationEngine = "true" };
            await _socketClient.EmitAsync(SpeechInitialize, msg);
            _logger.Log(msg.ToString());
        }

        private async Task StopConversation()
        {
            await _socketClient.EmitAsync(SpeechEnd);
        }

        private void HandleConversationMessage(string responseJson)
        {
            var messages = JsonConvert.DeserializeObject<List<RoomDto.RoomMessage>>(responseJson);
            var roomMessage = messages.FirstOrDefault();
            var messageText = roomMessage?.action?.text?.text;

            if (!string.IsNullOrEmpty(messageText))
            {
                _logger.Log($"Got message [{roomMessage.participantType}]:{messageText}");
            }
            if (roomMessage.participantType == "EndUser")
            {
                _actionToken.UserActionFired?.Invoke(new UserAction(messageText));
            }
            else if ((roomMessage.participantType == "Api" || roomMessage.participantType == "User") && !string.IsNullOrEmpty(messageText))
            {
                RequestTTSProcessing?.Invoke(messageText, (data) => ProcessResponse(roomMessage, data));
            }
        }

        private void ProcessResponse(RoomDto.RoomMessage message, RoomDto.BeingVoiceData voiceData)
        {
            if (voiceData != null)
            {
                var action = new BeingAction
                {
                    text = message?.action?.text?.text,
                    speech = voiceData?.data,
                    marks = voiceData?.marks,
                    cards = message?.action?.uiAction?.value?.cards,
                    buttons = message?.action?.uiAction?.value?.buttons,
                };
                _actionToken.BeingActionFired?.Invoke(action);
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

        private class StateMessage
        {
            public string previous { get; set; }
            public string current { get; set; }

            public bool IndicateInitialization => previous == "socket" && current == "initialized";
        }
    }
}