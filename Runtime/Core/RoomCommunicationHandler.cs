﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Plugins.Virbe.Core.Api;
using Virbe.Core.Actions;
using Virbe.Core.Api;
using Virbe.Core.Logger;
using static Virbe.Core.CommunicationSystem;

namespace Virbe.Core
{
    internal sealed class RoomCommunicationHandler: ICommunicationHandler
    {
        bool ICommunicationHandler.Initialized => _initialized;

        private VirbeUserSession _currentUserSession;
        private bool _initialized;
        private readonly RequestActionType _definedActions =
            RequestActionType.SendText |
            RequestActionType.SendNamedAction | 
            RequestActionType.SendAudio;

        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(RoomCommunicationHandler));
        private readonly int _poolingInterval;
        private readonly IApiBeingConfig _config;

        private RoomApiService _roomApiService;
        private CancellationTokenSource _pollingMessagesCancelletion;
        private VirbeBeing _being;
        private ActionToken _callActionToken;

        internal RoomCommunicationHandler(VirbeBeing being, CommunicationSystem.ActionToken actionToken, int interval)
        {
            _poolingInterval = interval;
            _config = being.ApiBeingConfig;
            _being = being;
            _being.ConversationStarted += StartCommunication;
            _being.ConversationEnded += StartCommunication;
            _callActionToken = actionToken;
        }

        bool ICommunicationHandler.HasCapability(RequestActionType type) => HasCapability(type);

        async Task ICommunicationHandler.Prepare(VirbeUserSession session)
        {
            EndCommunication();
            _currentUserSession = session;
            _roomApiService = _config.CreateRoomObject(_currentUserSession.UserId);

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

        private bool HasCapability(RequestActionType type) => (_definedActions & type) == type;

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
            if (!_config.RoomData.Enabled)
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
                            _callActionToken.UserActionFired?.Invoke(new UserAction(message.action?.text?.text));
                            await UniTask.SwitchToTaskPool();
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
                                        text = messageText,
                                        speech = getVoiceTask.Result.data,
                                        marks = getVoiceTask.Result.marks,
                                        cards = message?.action?.uiAction?.value?.cards,
                                        buttons = message?.action?.uiAction?.value?.buttons,
                                    };
                                    await UniTask.SwitchToMainThread();
                                    _callActionToken.BeingActionFired?.Invoke(action);
                                    await UniTask.SwitchToTaskPool();
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
            _callActionToken = null;
            _currentUserSession = null;
            _roomApiService = null;
            _being.ConversationStarted -= StartCommunication;
            _being.ConversationEnded -= StartCommunication;
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
            }
        }
    }
}