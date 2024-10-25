using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using Virbe.Core.Actions;
using Virbe.Core.Custom;
using Virbe.Core.Data;
using Virbe.Core.Logger;
using static Virbe.Core.Handlers.CommunicationSystem;

namespace Virbe.Core.Handlers
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

        public readonly RequestActionType DefinedActions = RequestActionType.SendAudioStream | RequestActionType.SendText;
        private readonly LocalizationData _localizationData;
        private readonly ConnectionType _connectionType;
        private readonly ActionToken _actionToken;
        private readonly ConversationData _data;
        private readonly string _baseUrl;

        private bool _initialized;
        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(ConversationSocketCommunicationHandler));
        private SocketIOClient.SocketIO _socketClient;
        private ConcurrentQueue<byte[]> _speechBytesAwaitingSend = new ConcurrentQueue<byte[]>();
        private StringBuilder _currentSttResult = new StringBuilder();

        private CancellationTokenSource _endlessSocketTokenSource;
        private CancellationTokenSource _audioSocketSenderTokenSource;
        private VirbeUserSession _currentSession;
        private Action _additionalDisposeAction;
        private Action<Dictionary<string, string>> _updateHeader;
        private ConcurrentQueue<SpeechChunk> _speechChunks = new ConcurrentQueue<SpeechChunk>();
        private DateTime _lastMessageTime;

        internal ConversationSocketCommunicationHandler(string baseUrl, ConversationData data, ActionToken actionToken, ConnectionType connectionType, LocalizationData localizationData)
        {
            _baseUrl = baseUrl;
            _data = data;
            _actionToken = actionToken;
            _connectionType = connectionType;
            _localizationData = localizationData;
        }

        bool ICommunicationHandler.HasCapability(RequestActionType type)
        {
            return (DefinedActions & type) == type;
        }

        async Task ICommunicationHandler.Prepare(VirbeUserSession session)
        {
            if (_initialized)
            {
                await CloseSocket();
            }
            ClearProcessingQueue();
            _initialized = true;
            _currentSession = session;

            await ConnectToSocket();
        }

        Task ICommunicationHandler.MakeAction(RequestActionType type, params object[] args)
        {
            if (!_socketClient.Connected)
            {
                _logger.Log($"Could not make action {type}, because socket is not connected");
                return Task.CompletedTask;
            }
            if (type == RequestActionType.SendAudioStream)
            {
                SendSpeech(args[0] as byte[]);
            }
            else if (type == RequestActionType.SendText)
            {
                SendText(args[0] as string).Forget();
            }
            return Task.CompletedTask;
        }

        internal void StartSendingSpeech() => StartSpeech().Forget();

        internal void StopSendingSpeech() => StopSpeech().Forget();

        internal void SetAdditionalDisposeAction(Action callback) => _additionalDisposeAction = callback;

        internal void SetHeaderUpdate(Action<Dictionary<string,string>> updateHeaderAction)
        {
            _updateHeader = updateHeaderAction;
        }

        private async UniTask CloseSocket()
        {
            _audioSocketSenderTokenSource?.Cancel();
            await DisposeSocketConnection();
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

        private async UniTask DisposeSocketConnection()
        {
            if (_socketClient?.Connected == true)
            {
                await DisconnectFromSocket();
            }
            else
            {
                _speechBytesAwaitingSend?.Clear();
                _speechChunks?.Clear();
                _endlessSocketTokenSource?.Cancel();
            }
        }

        private Task ConnectToSocket()
        {
            _endlessSocketTokenSource?.Cancel();
            _endlessSocketTokenSource = new CancellationTokenSource();
            _socketClient = new SocketIOClient.SocketIO(_baseUrl);
            _socketClient.Options.EIO = SocketIO.Core.EngineIO.V4;
            _socketClient.Options.Path = _data.Path;
            _socketClient.Options.Transport = SocketIOClient.Transport.TransportProtocol.WebSocket;
            _socketClient.Options.Reconnection = true;
            _socketClient.Options.ReconnectionDelay = 2000f;
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
                _logger.Log($"[{DateTime.Now}] Conversation state changed : {response}");
                var responseMessage = response.ToString();
                var state = JsonConvert.DeserializeObject<List<StateMessage>>(responseMessage);
            });

            _socketClient.On(ConversationInitialize, (response) => OnConversationInitialize(response.ToString()));

            _socketClient.OnConnected += (sender, args) => OnConnected().Forget();

            _socketClient.OnDisconnected += (sender, args) =>
            {
                _endlessSocketTokenSource?.Cancel();
                _logger.Log($"Disconnected from the conversation socket {args}");
                _actionToken.ConversationDisconnected?.Invoke();
            };

            _socketClient.OnError += (sender, args) => _logger.Log($"Socket error: {args}");

            _socketClient.OnReconnectAttempt += OnReconnectingHandler;
            _socketClient.OnReconnected += OnReconnectedHandler;
            _socketClient.On(ConversationError, (response) => _logger.Log($"[{DateTime.Now}]Conversation error occured : {response}"));

            _logger.Log($"Try connecting to conversation socket endpoint: {_baseUrl}{_data.Path}");
            return _socketClient.ConnectAsync(_endlessSocketTokenSource.Token);
        }

        private void OnReconnectedHandler(object sender, int e)
        {
            _actionToken.ConversationConnected?.Invoke();
        }

        private void OnReconnectingHandler(object sender, int attemptCount)
        {
            _logger.Log($"Reconnecting, attempt: {attemptCount}");
            _actionToken.ConversationReconnecting?.Invoke();
        }

        private void OnConversationInitialize(string responseJson)
        {
            List<ConversationMessage> messages = new List<ConversationMessage>();

            try
            {
                var conversation = JsonConvert.DeserializeObject<List<ConversationStartResponse>>(responseJson);
                if(conversation.Count == 0)
                {
                    Debug.LogError("Conversation init message is empty");
                    return;
                }
                _currentSession.UpdateSession(_currentSession.UserId, conversation[0].conversation.Id);
                _logger.Log($"[{DateTime.Now}]Conversation initialized with ID : {_currentSession.ConversationId} for user : {_currentSession.UserId}");
                messages = conversation[0].messages;
            }
            catch (Exception ex)
            {
                Debug.LogError("Could not parse conversation init message: " + ex);
            }

            if (_connectionType == ConnectionType.Continous)
            {
                StartSpeech().Forget();
            }

            foreach (var message in messages)
            {
                if (message.ParticipantType == "Api" || message.ParticipantType == "User")
                {
                    //TODO: uncomment when backend will properly filter messages by time
                    //ProcessMessage(message);
                }
            }
        }

        private async UniTaskVoid OnConnected()
        {
            _speechBytesAwaitingSend?.Clear();
            _speechChunks?.Clear();
            _logger.Log($"Connected to the socket.");
            _actionToken.ConversationConnected?.Invoke();
            await InitializeConversation(_currentSession.ConversationId);
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

        private async Task InitializeConversation(string conversationID = null)
        {
            _logger.Log($"Initializing conversation.");
            if (string.IsNullOrEmpty(_currentSession.UserId))
            {
                _logger.LogError($"{nameof(_currentSession.UserId)} must be UUID.");
                return;
            }
            var msg = new InitializeMessage() { endUserId = _currentSession.UserId};
            if (!string.IsNullOrEmpty(conversationID))
            {
                msg.conversationId = conversationID;
                msg.messageSince = _lastMessageTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
                _logger.Log($"Restoring conversation for ID: {conversationID} and time :{msg.messageSince}");
            }
            var dict = new Dictionary<string, string>();
            _updateHeader?.Invoke(dict);
            msg.profileId = dict["x-virbe-access-key"];
            msg.profileAccessSecret = dict["x-virbe-access-secret"];
            await _socketClient.EmitAsync(ConversationInitialize, msg);
        }

        private async Task InitializeSpeech()
        {
            _speechBytesAwaitingSend.Clear();
            _speechChunks.Clear();
            var msg = new SpeachInitMessage() {enableContinuousRecognition = "true", sendResultsToConversationEngine = "true" };
            await _socketClient.EmitAsync(SpeechInitialize, msg);
        }

        private void HandleConversationMessage(string responseJson)
        {
            List<ConversationMessage> messages = new List<ConversationMessage>();
            try
            {
                messages = JsonConvert.DeserializeObject<List<ConversationMessage>>(responseJson);
                if (messages.Count == 0)
                {
                    Debug.Log("Messages list empty but get conversation message");
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Could not parse conversation message: " + ex);
            }
            _lastMessageTime = DateTime.UtcNow;
            ProcessMessage(messages.FirstOrDefault());
        }

        private void ProcessMessage(ConversationMessage message)
        {
            var messageText = message?.Action?.Text?.Text;

            if (!string.IsNullOrEmpty(messageText))
            {
                _logger.Log($"Got message [{message.ParticipantType}]:{messageText}");
            }

            if (message.ParticipantType == "EndUser")
            {
                _actionToken.UserActionExecuted?.Invoke(new UserAction(messageText));
            }
            else if (message.ParticipantType == "Api" || message.ParticipantType == "User")
            {
                HandleApiMessage(message);
            }
        }

        private void HandleApiMessage(ConversationMessage message)
        {
            var messageText = message?.Action?.Text?.Text;
            CallEventsForNonEmptyActions(message);

            if (!string.IsNullOrEmpty(messageText))
            {
                var guid = Guid.NewGuid();

                var ttsProcessingArgs = new TTSProcessingArgs(messageText, guid, message?.Action?.Text?.Language, null, (data) => ProcessResponse(guid, data));
                _speechChunks.Enqueue(new SpeechChunk(ttsProcessingArgs, message));
                RequestTTSProcessing?.Invoke(ttsProcessingArgs);
            }
        }

        private void CallEventsForNonEmptyActions(ConversationMessage message)
        {
            if (message.Action.CustomAction != null)
            {
                _actionToken?.CustomActionExecuted(message.Action.CustomAction);
            }
            if (message.Action.UiAction != null)
            {
                _actionToken?.UiActionExecuted(message.Action.UiAction);
            }
            if (message.Action.BehaviorAction != null)
            {
                _actionToken?.BehaviourActionExecuted(message.Action.BehaviorAction);
            }
            if (message.Action.NamedAction != null)
            {
                _actionToken?.NamedActionExecuted(message.Action.NamedAction);
            }
            if (message.Action.Signal != null)
            {
                _actionToken?.SignalExecuted(message.Action.Signal);
            }
            if (message.Action.EngineEvent != null)
            {
                _actionToken?.EngineEventExecuted(message.Action.EngineEvent);
            }
        }

        private void ProcessResponse(Guid guid, VoiceData voiceData)
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
                        cards.Add( new Card() { Title = cardItem.Title, Payload = cardItem.Payload, PayloadType = cardItem.PayloadType });
                    }
                    var buttons = new List<Button>();
                    foreach (var buttonItem in result.Message?.Action?.UiAction?.Value?.Buttons ?? new List<VirbeButton>())
                    {
                        buttons.Add(new Button() { Title = buttonItem.Label, Payload = buttonItem.Payload, PayloadType = buttonItem.PayloadType});
                    }

                    var action = new BeingAction
                    {
                        text = result.Message?.Action?.Text?.Text,
                        speech = result.VoiceData?.Data,
                        marks = result.VoiceData?.Marks,
                        cards = cards,
                        buttons = buttons,
                        audioParameters = result.VoiceData?.AudioParameters,
                    };
                    _actionToken.BeingActionExecuted?.Invoke(action);
                }
            }
        }

        private async UniTask DisconnectFromSocket()
        {
            _endlessSocketTokenSource?.Cancel();
            await _socketClient.EmitAsync(SpeechEnd);
            await _socketClient.DisconnectAsync();
            _socketClient.Dispose();
            _socketClient = null;
        }

        void IDisposable.Dispose()
        {
            _initialized = false;
            CloseSocket().Forget();
            ClearProcessingQueue();
            _additionalDisposeAction?.Invoke();
        }

        async Task ICommunicationHandler.ClearProcessingQueue()
        {
            ClearProcessingQueue();
            if (_initialized)
            {
                //TODO: call here conversation cancel instead of reconnect
                await CloseSocket();
                await Task.Delay(250);
                _lastMessageTime = DateTime.UtcNow;
                await ConnectToSocket();
            }
        }

        private void ClearProcessingQueue()
        {
            _speechBytesAwaitingSend.Clear();
            _currentSttResult.Clear();
            _speechChunks.Clear();
        }

        private class SpeechChunk
        {
            public TTSProcessingArgs ProcessingArgs { get; }
            public DateTime CreatedAt { get; }
            public ConversationMessage Message { get; }
            public VoiceData VoiceData { get; set; }

            public SpeechChunk(TTSProcessingArgs args, ConversationMessage message)
            {
                ProcessingArgs = args;
                Message = message;
                CreatedAt = DateTime.UtcNow;
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
            public string profileId { get; set; }
            public string profileAccessSecret { get; set; }
            public string endUserId { get; set; }
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string conversationId { get; set; }
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string messageSince { get; set; }

            public override string ToString()
            {
                return $"endUserId = {endUserId},";
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
        }

        private class ConversationStartResponse
        {
            public Conversation conversation { get; set; }
            public List<ConversationMessage> messages { get; set; }
        }
    }
}