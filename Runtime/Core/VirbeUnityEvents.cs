using System;
using UnityEngine.Events;
using Virbe.Core.Actions;
using Virbe.Core.Custom;

namespace Virbe.Core
{
    [Serializable]
    public class BeingStateChangeEvent : UnityEvent<BeingState>
    {
    }

    [Serializable]
    public class UserActionEvent : UnityEvent<UserAction>
    {
    }


    [Serializable]
    public class BeingActionEvent : UnityEvent<BeingAction>
    {
    }

    [Serializable]
    public class ConversationErrorEvent : UnityEvent<Exception>
    {
    }
    [Serializable]
    public class TextSubmitEvent : UnityEvent<string>
    {
    }

    [Serializable]
    public class ProductLearnMoreEvent : UnityEvent<Card>
    {
    }

    [Serializable]
    public class QuickReplyEvent : UnityEvent<Button>
    {
    }

    [Serializable]
    public class SubmitInputEvent : UnityEvent<Core.Custom.Input, string>
    {
    }

    [Serializable]
    public class CancelInputEvent : UnityEvent<Core.Custom.Input>
    {
    }
}
