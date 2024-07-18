using System;
using Virbe.Core;
using Virbe.Core.Actions;

namespace Virbe.UI.Components
{
    // TODO merge with LayoutConsumeBeingEvent
    public abstract class VirbeUIComponent : VirbeBeingEventConsumer
    {
        public override void UserActionPlayed(UserAction beingAction)
        {
            // Most times you won't need to implement
        }

        public override void ConversationErrorHappened(Exception exception)
        {
            // Most times you won't need to implement
        }

        public abstract void SetVisible(bool visible);
    }
}