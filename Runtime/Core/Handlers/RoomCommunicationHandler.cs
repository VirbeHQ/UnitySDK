using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Virbe.Core.Actions;
using Virbe.Core.Data;
using Virbe.Core.Logger;
using Virbe.Core.RoomApi;
using static Virbe.Core.Handlers.CommunicationSystem;

namespace Virbe.Core.Handlers
{
    internal sealed class RoomCommunicationHandler: ICommunicationHandler
    {
        bool ICommunicationHandler.Initialized => _initialized;

        private VirbeUserSession _currentUserSession;
        private bool _initialized;
        public readonly RequestActionType DefinedActions =
            RequestActionType.SendText |
            RequestActionType.SendNamedAction |
            RequestActionType.SendAudio;

        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(RoomCommunicationHandler));
        private readonly int _poolingInterval;

        private RoomApiService _roomApiService;
        private CancellationTokenSource _pollingMessagesCancelletion;
        private ActionToken _callActionToken;
        private RoomData _roomData;
        private Action _additionalDisposeAction;
        private TTSData _ttsData;

        internal RoomCommunicationHandler(RoomData data, ActionToken actionToken, TTSData ttsData, int interval = 500)
        {
            _poolingInterval = interval;
            _roomData = data;
            _callActionToken = actionToken;
            _ttsData = ttsData;
        }

        bool ICommunicationHandler.HasCapability(RequestActionType type) => HasCapability(type);

        async Task ICommunicationHandler.Prepare(VirbeUserSession session)
        {
            EndCommunication();
            _currentUserSession = session;
            _roomApiService = _roomData.CreateRoomObject(_currentUserSession.UserId);

            if (string.IsNullOrEmpty(_currentUserSession.ConversationId))
            {
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
                    _currentUserSession.UpdateSession(_currentUserSession.UserId, createdRoom.id);
                }
            }
            else
            {
                _roomApiService.OverrrideRoomId(_currentUserSession.ConversationId);
            }
          
            _initialized = true;
        }

        private bool HasCapability(RequestActionType type) => (DefinedActions & type) == type;

        internal void StartCommunication()
        {
            EndCommunication();
            _pollingMessagesCancelletion = new CancellationTokenSource();
            MessagePollingTask(_pollingMessagesCancelletion.Token).Forget();
        }

        internal void EndCommunication()
        {
            _pollingMessagesCancelletion?.Cancel();
        }

        private async Task SendSpeech(byte[] recordedAudioBytes) 
        {
            var sendTask = _roomApiService.SendSpeech(recordedAudioBytes);
            await sendTask;

            if (sendTask.IsFaulted)
            {
                _logger.Log("Failed to send speech: " + sendTask.Exception?.Message);
            }
            else if (sendTask.IsCompleted)
            {
                _logger.Log("Sent speech");
            }
        }

        private async Task SendNamedAction(string name, string value)
        {
            var sendTask = _roomApiService.SendNamedAction(name, value);
            await sendTask;

            if (sendTask.IsFaulted)
            {
                _logger.Log("Failed to send namedAction: " + sendTask.Exception?.Message);
            }
            else if (sendTask.IsCompleted)
            {
                _logger.Log("Sent namedAction");
            }
        }

        private async Task SendText(string text)
        {
            var sendTask = _roomApiService.SendText(text);
            await sendTask;

            if (sendTask.IsFaulted)
            {
                _logger.Log("Failed to send text: " + sendTask.Exception?.Message);
            }
            else if (sendTask.IsCompleted)
            {
                _logger.Log("Sent text");
            }
        }

        private async UniTaskVoid MessagePollingTask(CancellationToken cancellationToken)
        {
            await UniTask.SwitchToTaskPool();
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_poolingInterval);

                if (_roomApiService == null || !_initialized)
                {
                    return;
                }

                try
                {
                    var getMessagesTask = _roomApiService.PollNewMessages();
                    await getMessagesTask;

                    if (getMessagesTask.IsFaulted)
                    {
                        // Handle any errors that occurred during room creation
                        _logger.LogError("Failed to get messages: " + getMessagesTask.Exception?.Message);
                        continue;
                    }

                    if (!getMessagesTask.IsCompleted)
                    {
                        // Handle any errors that occurred during room creation
                        _logger.LogError("Taks has been awaited but is not completed");
                        continue;
                    }

                    // Get the messages from the task result
                    var messages = getMessagesTask.Result;

                    if (messages == null || messages.count == 0)
                    {
                        continue;
                    }

                    _logger.Log($"Got new messages: {messages?.count}");
                    messages.results.Reverse();
                    foreach (var message in messages.results)
                    {
                        var messageText = message?.action?.text?.text;
                        if (!string.IsNullOrEmpty(messageText))
                        {
                            _logger.Log("Got message: " + messageText);
                        }
                        if (message.participantType == "EndUser")
                        {
                            await UniTask.SwitchToMainThread();
                            _callActionToken.UserActionExecuted?.Invoke(new UserAction(message.action?.text?.text));
                            await UniTask.SwitchToTaskPool();
                        }
                        else if (message.participantType == "Api" || message.participantType == "User")
                        {
                            try
                            {
                                var voiceResult = await _roomApiService.GetRoomMessageVoiceData(message);
                                if (voiceResult != null)
                                {
                                    ProcessResponse(message, voiceResult.marks, voiceResult.data);
                                }
                            }
                            catch (Exception e)
                            {
                                _logger.LogError("Failed to get voice data: " + e.Message);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError("Failed to get messages: " + e.Message);
                }
            }
        }

        private void ProcessResponse(RoomDto.RoomMessage message, VoiceData voiceData)
        {
            if (voiceData != null)
            {
                ProcessResponse(message, voiceData.Marks, voiceData?.Data);
            }
        }

        private void ProcessResponse(RoomDto.RoomMessage message, List<Mark> marks, byte[] data)
        {
            var action = new BeingAction
            {
                text = message?.action?.text?.text,
                speech = data,
                marks = marks,
                cards = message?.action?.uiAction?.value?.cards,
                buttons = message?.action?.uiAction?.value?.buttons,
                audioParameters = new AudioParameters() { Channels = _ttsData.AudioChannels, Frequency = _ttsData .AudioFrequency, SampleBits = _ttsData.AudioSampleBits },
            };
            _callActionToken.BeingActionExecuted?.Invoke(action);
        }

        void IDisposable.Dispose()
        {
            _initialized = false;
            EndCommunication();
            _additionalDisposeAction?.Invoke();
            _callActionToken = null;
            _currentUserSession = null;
            _roomApiService = null;
        }

        async Task ICommunicationHandler.MakeAction(RequestActionType type, params object[] args)
        {
            if (!_initialized)
            {
                _logger.Log("Handler not initialized");
                return;
            }
            if(!HasCapability(type))
            {
                _logger.Log($"Handler {nameof(RoomCommunicationHandler)} does not support action {type}");
                return;
            }
            switch (type)
            {
                case RequestActionType.SendText:
                    await _roomApiService.SendText(args[0] as string); 
                    break;
                case RequestActionType.SendNamedAction:
                    var value = args.Length >1 ? args[1] : null;
                    await _roomApiService.SendNamedAction(args[0] as string, value as string);
                    break;
                case RequestActionType.SendAudio:
                    await _roomApiService.SendSpeech(args[0] as byte[]);
                    break;
                default:
                    _logger.Log($"Handler {nameof(RoomCommunicationHandler)} does not support action {type}");
                    break;
            }
        }

        internal void SetAdditionalDisposeAction(Action value)
        {
            _additionalDisposeAction = value;
        }

        Task ICommunicationHandler.ClearProcessingQueue()
        {
            return Task.CompletedTask;
        }
    }
}