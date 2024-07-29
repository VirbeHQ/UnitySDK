using Cysharp.Threading.Tasks;
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
                var roomHandler = new RoomCommunicationHandler(_being, _callActionToken, 500);
                _handlers.Add(roomHandler);
            }
            if (_apiBeingConfig.SttProtocol == SttConnectionProtocol.socket_io)
            {
                var socketHandler = new STTSocketCommunicationHandler(being);
                socketHandler.RequestTextSend += (text) => MakeAction(RequestActionType.SendText, text).Forget(); 
                _handlers.Add(socketHandler);
            }
        }

        internal async UniTask InitializeWith(string endUserId = null, string conversationId= null)
        {
            _session = new VirbeUserSession(endUserId, conversationId);
            foreach (var handler in _handlers)
            {
                await handler.Prepare(_session);
            }
            Initialized = true;
        }

        internal async UniTask MakeAction(RequestActionType type, params object[] args)
        {
            foreach (var handler in _handlers)
            {
                if (handler.Initialized && handler.HasCapability(type))
                {
                    await handler.MakeAction(type, args);
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
        SendAudio = 2 ,
        SendAudioStream = 4 
    }
}