using Virbe.Core.Actions;

namespace Virbe.Core
{
    public class BeingState
    {
        protected internal Behaviour _behaviour;
        protected internal bool isMuted;

        protected internal UserAction lastUserAction;
        protected internal BeingAction lastBeingAction;

        public BeingState()
        {
            _behaviour = Behaviour.Idle;
            isMuted = false;
        }

        public Behaviour Behaviour => _behaviour;

        public bool IsMuted => isMuted;
    }

    public enum Behaviour
    {
        Idle,
        Focused,
        InConversation,
        Listening,
        RequestProcessing,
        RequestError,
        RequestReceived,
        PlayingBeingAction
    }
}