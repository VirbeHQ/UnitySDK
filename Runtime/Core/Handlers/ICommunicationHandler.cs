using Cysharp.Threading.Tasks;
using System;
using System.Threading.Tasks;
using Virbe.Core.Api;

namespace Virbe.Core
{
    internal interface ICommunicationHandler: IDisposable
    {
        bool Initialized { get; }
        public bool HasCapability(RequestActionType type);

        Task Prepare(VirbeUserSession session);
        UniTask MakeAction(RequestActionType type, params object[] args);
    }
}