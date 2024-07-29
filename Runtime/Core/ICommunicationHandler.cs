using System;
using System.Threading.Tasks;
using Virbe.Core.Actions;
using Virbe.Core.Api;

namespace Virbe.Core
{
    internal interface ICommunicationHandler: IDisposable
    {
        bool Initialized { get; }
        public bool HasCapability(RequestActionType type);

        Task Prepare(VirbeUserSession session);
        Task MakeAction(RequestActionType type, params object[] args);
    }
}