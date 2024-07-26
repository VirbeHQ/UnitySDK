using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Plugins.Virbe.Core.Api;
using Virbe.Core.Actions;
using Virbe.Core.Api;
using Virbe.Core.Logger;


namespace Virbe.Core
{
    internal sealed class RestCommunicationHandler: ICommunicationHandler
    {
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

        bool ICommunicationHandler.Initialized => _initialized;
        bool ICommunicationHandler.AudioStreamingEnabled =>false;

        private VirbeUserSession _currentUserSession;
        private bool _initialized;

        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(RestCommunicationHandler));
        private readonly int _poolingInterval;
        private readonly IApiBeingConfig _config;

        private RoomApiService _roomApiService;
        private CancellationTokenSource _pollingMessagesCancelletion;
        private event Action<BeingAction> BeingActionFired;
        private event Action<UserAction> UserActionFired;
        private VirbeBeing _being;

        internal RestCommunicationHandler(VirbeBeing being,int interval)
        {
            _poolingInterval = interval;
            _config = being.ApiBeingConfig;
            _being = being;
            _being.ConversationStarted += StartCommunication;
            _being.ConversationEnded += StartCommunication;
        }

        async Task ICommunicationHandler.Prepare(string userId, string conversationId)
        {
            EndCommunication();
            _currentUserSession = new VirbeUserSession(userId, conversationId);
            if(string.IsNullOrEmpty(conversationId))
            {
                _roomApiService = _config.CreateRoom(_currentUserSession.EndUserId);
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
                    _currentUserSession.UpdateSession(_currentUserSession.EndUserId, createdRoom.id);
                }
            }
          
            _initialized = true;
        }

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

        async Task ICommunicationHandler.SendSpeech(byte[] recordedAudioBytes) 
        {
            if (!_config.RoomEnabled)
            {
                return;
            }
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

        Task ICommunicationHandler.SendNamedAction(string name, string value) => _roomApiService.SendNamedAction(name,value);

        Task ICommunicationHandler.SendText(string text) => _roomApiService.SendText(text);

        private async UniTaskVoid MessagePollingTask(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_poolingInterval);

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
                        _logger.Log("Got message: " + message?.action?.text?.text);
                        if (message.participantType == "EndUser")
                        {
                            await UniTask.SwitchToMainThread();
                            UserActionFired?.Invoke(new UserAction(message.action?.text?.text));
                        }
                        else if (message.participantType == "Api" || message.participantType == "User")
                        {
                            try
                            {
                                //todo: move this to tts
                                var getVoiceTask = _roomApiService.GetRoomMessageVoiceData(message);
                                await getVoiceTask;

                                if (getVoiceTask.IsFaulted)
                                {
                                    // TODO should we try to get the voice data again?
                                    // Handle any errors that occurred during room creation
                                    _logger.LogError("Failed to get voice data: " +
                                                   getVoiceTask.Exception?.Message);
                                }
                                else if (getVoiceTask.IsCompleted)
                                {
                                    var action = new BeingAction
                                    {
                                        text = message?.action?.text.text,
                                        speech = getVoiceTask.Result.data,
                                        marks = getVoiceTask.Result.marks,
                                        cards = message?.action?.uiAction?.value?.cards,
                                        buttons = message?.action?.uiAction?.value?.buttons,
                                    };
                                    BeingActionFired?.Invoke(action);
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

        void IDisposable.Dispose()
        {
            _initialized = false;
            EndCommunication();
            UserActionFired = null;
            BeingActionFired = null;
            _currentUserSession = null;
            _roomApiService = null;
            _being.ConversationStarted -= StartCommunication;
            _being.ConversationEnded -= StartCommunication;
        }
    }
}