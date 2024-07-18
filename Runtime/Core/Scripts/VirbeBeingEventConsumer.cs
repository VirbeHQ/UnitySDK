using System;
using UnityEngine;
using Virbe.Core.Actions;

namespace Virbe.Core
{
    public abstract class VirbeBeingEventConsumer : MonoBehaviour, IVirbeBeingEvetConsumer
    {
        public abstract void BeingStateChanged(BeingState beingState);
        public abstract void UserActionPlayed(UserAction userAction);
        public abstract void BeingActionPlayed(BeingAction beingAction);
        public abstract void ConversationErrorHappened(Exception exception);
    }
}