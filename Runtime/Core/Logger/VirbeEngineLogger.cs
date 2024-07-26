using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Virbe.Core.Logger
{
    internal class VirbeEngineLogger
    {
        private static Thread _mainThread = Thread.CurrentThread;
        private static readonly ILogger Logger = Debug.unityLogger;

        private const string Tag = "VirbeEngine";

        private readonly string _prefix;

        public VirbeEngineLogger(string prefix)
        {
            _prefix = prefix;
        }

        internal void Log(string format, params object[] args) => LogOnMainThread(LogType.Log, format, args).Forget();
        internal void LogError(string format, params object[] args) => LogOnMainThread(LogType.Error, format, args).Forget();
        private async UniTaskVoid LogOnMainThread(LogType logType, string format, params object[] args)
        {
            if (Thread.CurrentThread != _mainThread)
            {
                await UniTask.SwitchToMainThread();
            }
            Logger.LogFormat(logType, $"{Tag}/{_prefix}: " + format, args);
        }
    }
}
