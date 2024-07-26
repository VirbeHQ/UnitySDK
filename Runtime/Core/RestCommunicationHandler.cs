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
    internal class RestCommunicationHandler: IDisposable
    {
        public event Action<UserAction> UserActionFired;
        public event Action<BeingAction> BeingActionFired;

        internal bool Initialized { get; private set; }
        internal VirbeUserSession CurrentUserSession { get; private set; }

        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(RestCommunicationHandler));
        private readonly int _poolingInterval;
        private readonly IApiBeingConfig _config;

        private RoomApiService _roomApiService;
        private CancellationTokenSource _pollingMessagesCancelletion;

        internal RestCommunicationHandler(IApiBeingConfig config, int interval)
        {
            _poolingInterval = interval;
            _config = config;
        }

        internal async Task Prepare(string userId = null, string conversationId = null)
        {
            CurrentUserSession = new VirbeUserSession(userId, conversationId);
            if(string.IsNullOrEmpty(conversationId))
            {
                _roomApiService = _config.CreateRoom(CurrentUserSession.EndUserId);
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

        internal void StartCommunicatoin()
        {
            EndCommunication();
            _pollingMessagesCancelletion = new CancellationTokenSource();
            MessagePollingTask(_pollingMessagesCancelletion.Token).Forget();
        }

        internal void EndCommunication()
        {
            _pollingMessagesCancelletion?.Cancel();
        }

        internal Task SendSpeech(byte[] recordedAudioBytes) => _roomApiService.SendSpeech(recordedAudioBytes);

        internal Task SendNamedAction(string name, string value = null) => _roomApiService.SendNamedAction(name,value);

        internal Task SendText(string text) => _roomApiService.SendText(text);

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

        public void Dispose()
        {
            Initialized = false;
            EndCommunication();
            UserActionFired = null;
            BeingActionFired = null;
            CurrentUserSession = null;
            _roomApiService = null;
        }
    }
}