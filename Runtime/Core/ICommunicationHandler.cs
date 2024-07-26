using System;
using System.Threading.Tasks;
using Virbe.Core.Actions;

namespace Virbe.Core
{
    internal interface ICommunicationHandler: IDisposable
    {
        event Action<UserAction> UserActionFired;
        event Action<BeingAction> BeingActionFired;

        bool Initialized { get; }
        bool AudioStreamingEnabled { get; }

        Task Prepare(string userId = null, string conversationId = null);
        Task SendSpeech(byte[] speech);
        Task SendNamedAction(string name, string value = null);
        Task SendText(string text);
    }
}