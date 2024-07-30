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

            if (_apiBeingConfig.HasRoom && _apiBeingConfig.EngineType == EngineType.Room)
            {
                var roomSendingAudio = _apiBeingConfig.SttProtocol == SttConnectionProtocol.http;
                var roomHandler = new RoomCommunicationHandler(_apiBeingConfig, _callActionToken, roomSendingAudio, 500);
                _being.ConversationStarted += roomHandler.StartCommunication;
                _being.ConversationEnded += roomHandler.EndCommunication;
                roomHandler.RequestTTSProcessing += (text, callback) => ProcessTTS( text, callback).Forget();

                _handlers.Add(roomHandler);
            }
            if (_apiBeingConfig.SttProtocol == SttConnectionProtocol.socket_io)
            {
                var socketHandler = new STTSocketCommunicationHandler(_apiBeingConfig);
                _being.UserStartSpeaking += socketHandler.OpenSocket;
                _being.UserStopSpeaking += socketHandler.CloseSocket;
                socketHandler.RequestTextSend += (text) => SendText( text).Forget(); 
                _handlers.Add(socketHandler);
            }
            if(_apiBeingConfig.TTSData.TtsConnectionProtocol == TtsConnectionProtocol.http)
            {
                var ttsRestHandler = new TTSCommunicationHandler(_apiBeingConfig);
                _handlers.Add(ttsRestHandler);
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

        //TODO: add class for tts processing
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