using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Plugins.Virbe.Core.Api;
using Virbe.Core.Actions;
using Virbe.Core.Api;
using Virbe.Core.Custom;
using Virbe.Core.Logger;
using static Virbe.Core.CommunicationSystem;

namespace Virbe.Core
{
    internal sealed class ConversationSocketCommunicationHandler : ICommunicationHandler
    {
        public event Action<TTSProcessingArgs> RequestTTSProcessing;

        private const string ConversationInitialize = "conversation-init";
        private const string ConversationMessage = "conversation-message";
        private const string SpeechRecognized = "speech-recognized";
        private const string SpeechInitialize = "speech-start";
        private const string SpeechEnd = "speech-end";
        private const string SpeechAudio = "speech-audio";
        private const string ConversationError = "conversation-error";
        private const string StateChanged = "state";

        bool ICommunicationHandler.Initialized => _initialized;
        private readonly RequestActionType _definedActions = RequestActionType.SendAudioStream | RequestActionType.SendText;

        private bool _initialized;
        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(ConversationSocketCommunicationHandler));
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
        private ConnectionType _connectionType;
        private Action _additionalDisposeAction;
        private ConcurrentQueue<SpeechChunk> _speechChunks = new ConcurrentQueue<SpeechChunk>();
        internal ConversationSocketCommunicationHandler(string baseUrl, ConversationData data, ActionToken actionToken, ConnectionType connectionType)
        {
            _baseUrl = baseUrl;
            _data = data;
            _supportedPayloads = data.SupportedPayloads;
            _actionToken = actionToken;
            _connectionType = connectionType;
        }

        bool ICommunicationHandler.HasCapability(RequestActionType type)
        {
            return (_definedActions & type) == type;
        }

        Task ICommunicationHandler.Prepare(VirbeUserSession session)
        {
            _initialized = true;
            _currentSession = session;
            return ConnectToSocket();

        }
        internal void StartSendingSpeech() => StartSpeech().Forget();

        internal void StopSendingSpeech() => StopSpeech().Forget();

        internal void SetAdditionalDisposeAction(Action callback) => _additionalDisposeAction = callback;

        private void CloseSocket()
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

        private async UniTaskVoid SendText(string text)
        {
            if (_socketClient == null)
            {
                _logger.LogError($"[VIRBE] Socket not created, could not send speech chunk");
                return;
            }
            var message = new SendTextMessage();
            message.action = new MessageAction();
            message.action.Text = new TextAction(text);
            await _socketClient.EmitAsync(ConversationMessage, message);
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

        private Task ConnectToSocket()
        {
            _endlessSocketTokenSource?.Cancel();
            _endlessSocketTokenSource = new CancellationTokenSource();
            _socketClient = new SocketIOClient.SocketIO(_baseUrl);
            _socketClient.Options.EIO = SocketIO.Core.EngineIO.V4;
            _socketClient.Options.Path = _data.Path;

            _currentSttResult.Clear();

            _socketClient.On(ConversationMessage, (response) => HandleConversationMessage(response.ToString()));

            _socketClient.On(SpeechRecognized, (response) => {

                var recognizedChunks = JsonConvert.DeserializeObject<List<RecognizedResponse>>(response.ToString());
                var firstChunk = recognizedChunks.FirstOrDefault();
                if (firstChunk != null) {

                    _logger.Log($"[{DateTime.Now}] Recognized text: {firstChunk.chunk}, language: {firstChunk.language}, final result: {firstChunk.isFinal}");
                    if (firstChunk.isFinal)
                    {
                        _actionToken?.UserSpeechRecognized(firstChunk.chunk);
                    }
                }
            });

            _socketClient.On(StateChanged, (response) =>
            {
                _logger.Log($"[{DateTime.Now}]Conversation state changed : {response}");
                var responseMessage = response.ToString();
                var state = JsonConvert.DeserializeObject<List<StateMessage>>(responseMessage);
                if (state.FirstOrDefault()?.IndicateInitialization == true)
                {
                    _logger.Log($"Conversation initialized");

                    if (_connectionType == ConnectionType.Continous)
                    {
                        StartSpeech().Forget();
                    }
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

        private async UniTaskVoid StartSpeech()
        {
            await InitializeSpeech();

            _audioSocketSenderTokenSource?.Cancel();
            _audioSocketSenderTokenSource = new CancellationTokenSource();
            SocketAudioSendLoop(_audioSocketSenderTokenSource.Token).Forget();
        }

        private async UniTaskVoid StopSpeech() 
        {
            await _socketClient.EmitAsync(SpeechEnd);
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
        }

        private async Task InitializeSpeech()
        {
            var msg = new SpeachInitMessage() {enableContinuousRecognition = "true", sendResultsToConversationEngine = "true" };
            await _socketClient.EmitAsync(SpeechInitialize, msg);
            _logger.Log(msg.ToString());
        }

        private void HandleConversationMessage(string responseJson)
        {
            var messages = JsonConvert.DeserializeObject<List<ConversationMessage>>(responseJson);
            var roomMessage = messages.FirstOrDefault();
            var messageText = roomMessage?.Action?.Text?.Text;

            if (!string.IsNullOrEmpty(messageText))
            {
                _logger.Log($"Got message [{roomMessage.ParticipantType}]:{messageText}");
            }
            if (roomMessage.ParticipantType == "EndUser")
            {
                _actionToken.UserActionFired?.Invoke(new UserAction(messageText));
            }
            else if ((roomMessage.ParticipantType == "Api" || roomMessage.ParticipantType == "User") && !string.IsNullOrEmpty(messageText))
            {
                var guid = Guid.NewGuid();
                var ttsProcessingArgs = new TTSProcessingArgs(messageText, guid, roomMessage?.Action?.Text?.Language, null, (data) => ProcessResponse(guid, data));
                _speechChunks.Enqueue(new SpeechChunk(ttsProcessingArgs, roomMessage));
                RequestTTSProcessing?.Invoke(ttsProcessingArgs);
            }
        }

        private void ProcessResponse(Guid guid, RoomDto.BeingVoiceData voiceData)
        {
            if (voiceData == null)
            {
                return;
            }
            //in case of wrong order of tts processing
            foreach (var chunk in _speechChunks)
            {
                if (chunk.ProcessingArgs.ID == guid)
                {
                    chunk.VoiceData = voiceData;
                }
            }
            while (_speechChunks.TryPeek(out var firstChunk))
            {
                if(firstChunk.VoiceData == null)
                {
                    return;
                }
                if (_speechChunks.TryDequeue(out var result))
                {
                    var cards = new List<Card>();
                    foreach (var cardItem in result.Message?.Action?.UiAction?.Value?.Cards ?? new List<VirbeCard>())
                    {
                        cards.Add(cardItem.ToOldCard());
                    }
                    var buttons = new List<Button>();
                    foreach (var cardItem in result.Message?.Action?.UiAction?.Value?.Buttons ?? new List<VirbeButton>())
                    {
                        cards.Add(cardItem.ToOldButton());
                    }
                    var action = new BeingAction
                    {
                        text = result.Message?.Action?.Text?.Text,
                        speech = result.VoiceData?.data,
                        marks = result.VoiceData?.marks,
                        cards = cards,
                        buttons = buttons,
                    };
                    _actionToken.BeingActionFired?.Invoke(action);
                }
            }
        }

        private async UniTaskVoid DisconnectFromSocket()
        {
            _endlessSocketTokenSource?.Cancel();
            var tempSocketHandle = _socketClient;
            await _socketClient.EmitAsync(SpeechEnd);
            await Task.Delay(250);
            await tempSocketHandle.DisconnectAsync();
            tempSocketHandle.Dispose();
            tempSocketHandle = null;
        }

        void IDisposable.Dispose()
        {
            _initialized = false;
            CloseSocket();
            _additionalDisposeAction?.Invoke();
        }

        UniTask ICommunicationHandler.MakeAction(RequestActionType type, params object[] args)
        {
            if (type == RequestActionType.SendAudioStream)
            {
                SendSpeech(args[0] as byte[]);
            }
            else if(type == RequestActionType.SendText)
            {
                SendText(args[0] as string).Forget();
            }
            return UniTask.CompletedTask;
        }

        private class SpeechChunk
        {
            public TTSProcessingArgs ProcessingArgs { get; }
            public ConversationMessage Message { get; }
            public RoomDto.BeingVoiceData VoiceData { get; set; }

            public SpeechChunk(TTSProcessingArgs args, ConversationMessage message)
            {
                ProcessingArgs = args;
                Message = message;
            }
        }

        private class SendTextMessage
        {
            public MessageAction action { get; set; }
        }

        private class RecognizedResponse
        {
            public string chunk { get; set; }
            public string language { get; set; }
            public bool isFinal { get; set; }

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