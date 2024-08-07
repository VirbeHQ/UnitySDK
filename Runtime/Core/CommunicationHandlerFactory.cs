using Cysharp.Threading.Tasks;
using Plugins.Virbe.Core.Api;
using System;
using System.Collections.Generic;
using Virbe.Core.Actions;
using Virbe.Core.Api;
using Virbe.Core.Logger;

namespace Virbe.Core
{
    internal sealed class CommunicationSystem: IDisposable
    {
        public event Action<UserAction> UserActionFired;
        public event Action<BeingAction> BeingActionFired;

        public bool Initialized { get; private set; }   

        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(CommunicationSystem));

        private List<ICommunicationHandler> _handlers =new List<ICommunicationHandler>();
        private VirbeUserSession _session;
        private IApiBeingConfig _apiBeingConfig;
        private VirbeBeing _being;
        private ActionToken _callActionToken;

        public CommunicationSystem(VirbeBeing being)
        {
            _apiBeingConfig = being.ApiBeingConfig;
            _being = being;
            _callActionToken = new ActionToken();
            _callActionToken.UserActionFired += (args) => UserActionFired?.Invoke(args);
            _callActionToken.BeingActionFired += (args) => BeingActionFired?.Invoke(args);

            var haveRoom = false;
            var supportedPayloads = new List<SupportedPayload>();
            foreach (var handler in _apiBeingConfig.ConversationData)
            {
                if(handler.ConnectionProtocol == ConnectionProtocol.http)
                {
                    var roomData = handler as RoomData;
                    var roomHandler = new RoomCommunicationHandler(roomData, _callActionToken, 500);
                    _being.ConversationStarted += roomHandler.StartCommunication;
                    _being.ConversationEnded += roomHandler.EndCommunication;
                    roomHandler.SetAdditionalDisposeAction(() =>
                    {
                        _being.ConversationStarted -= roomHandler.StartCommunication;
                        _being.ConversationEnded -= roomHandler.EndCommunication;
                    });
                    roomHandler.RequestTTSProcessing += (text, callback) => ProcessTTS(text, callback).Forget();

                    _handlers.Add(roomHandler);
                    haveRoom = true;
                }
                else if(handler.ConnectionProtocol == ConnectionProtocol.wsEndless)
                {
                  var endlessHandler = new EndlessSocketCommunicationHandler(_apiBeingConfig.BaseUrl, handler, _callActionToken);
                    endlessHandler.RequestTTSProcessing += (text, callback) => ProcessTTS(text, callback).Forget();
                    _handlers.Add(endlessHandler);
                }
                supportedPayloads.AddRange(handler.SupportedPayloads);
            }

            if(_apiBeingConfig.ConversationEngine == EngineType.Room && !haveRoom)
            {
                _logger.LogError($"Engine is set to room but no room provided. Could not initialize");
                return;
            }

            if (!supportedPayloads.Contains(SupportedPayload.SpeechAudio) )
            {
                if(_apiBeingConfig.FallbackSTTData.ConnectionProtocol == ConnectionProtocol.socket_io)
                {
                    var socketHandler = new STTSocketCommunicationHandler(_apiBeingConfig.BaseUrl, _apiBeingConfig.FallbackSTTData);
                    _being.UserStartSpeaking += socketHandler.OpenSocket;
                    _being.UserStopSpeaking += socketHandler.CloseSocket;
                    socketHandler.SetAdditionalDisposeAction(() =>
                    {
                        _being.UserStartSpeaking -= socketHandler.OpenSocket;
                        _being.UserStopSpeaking -= socketHandler.CloseSocket;
                    });
                    socketHandler.RequestTextSend += (text) => SendText(text).Forget();
                    _handlers.Add(socketHandler);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            if(_apiBeingConfig.ConversationEngine != EngineType.Room)
            {
                if (_apiBeingConfig.FallbackTTSData.ConnectionProtocol == ConnectionProtocol.http)
                {
                    var ttsRestHandler = new TTSCommunicationHandler(_apiBeingConfig.BaseUrl, _apiBeingConfig.FallbackTTSData, _apiBeingConfig.LocationId);
                    _handlers.Add(ttsRestHandler);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        internal async UniTask InitializeWith(string endUserId = null, string conversationId= null)
        {
            _session = new VirbeUserSession(endUserId, conversationId);
            foreach (var handler in _handlers)
            {
                try
                {
                    await handler.Prepare(_session);
                }
                catch (Exception _)
                {
                    _logger.Log($"Could not initialize {handler.GetType()}");
                    return;
                }
            }
            Initialized = true;
        }

        internal async UniTask SendText(string text)
        {
            foreach (var handler in _handlers)
            {
                if (handler.Initialized && handler.HasCapability(RequestActionType.SendText))
                {
                    await handler.MakeAction(RequestActionType.SendText, text);
                }
            }
        }

        internal async UniTask SendNamedAction(string name, string value = null)
        {
            foreach (var handler in _handlers)
            {
                if (handler.Initialized && handler.HasCapability(RequestActionType.SendNamedAction))
                {
                    await handler.MakeAction(RequestActionType.SendNamedAction, name, value);
                }
            }
        }

        internal async UniTask SendAudio(byte[] bytes, bool streamed)
        {
            var capability = streamed ? RequestActionType.SendAudioStream : RequestActionType.SendAudio;
            foreach (var handler in _handlers)
            {
                if (handler.Initialized && handler.HasCapability(capability))
                {
                    await handler.MakeAction(capability, bytes);
                }
            }
        }

        internal async UniTaskVoid ProcessTTS(string text, Action<RoomDto.BeingVoiceData> callback)
        {
            foreach (var handler in _handlers)
            {
                if (handler.Initialized && handler.HasCapability(RequestActionType.ProcessTTS))
                {
                    await handler.MakeAction(RequestActionType.ProcessTTS, text, callback);
                    return;
                }
            }
        }

        public void Dispose()
        {
            foreach(var handler in _handlers)
            {
                handler.Dispose();
            }
            UserActionFired = null;
            BeingActionFired = null;
            _handlers.Clear();
            Initialized = false;
        }

        internal class ActionToken
        {
            public Action<UserAction> UserActionFired;
            public Action<BeingAction> BeingActionFired;
        }
    }

    [Flags]
    public enum RequestActionType
    {
        SendText = 0,
        SendNamedAction = 1,
        SendAudio = 2,
        SendAudioStream = 4,

        ProcessTTS = 8,
    }
}