using System;
using Virbe.Core.Actions;

namespace Virbe.Core
{
    public interface IVirbeBeingEvetConsumer
    {
        void BeingStateChanged(BeingState beingState);
        void UserActionPlayed(UserAction userAction);
        void BeingActionPlayed(BeingAction beingAction);
        void ConversationErrorHappened(Exception exception);
    }
}