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

        internal void Log(string message) => LogOnMainThread(LogType.Log, message).Forget();
        internal void LogError(string message) => LogOnMainThread(LogType.Error, message).Forget();
        private async UniTaskVoid LogOnMainThread(LogType logType, string message)
        {
            if (Thread.CurrentThread != _mainThread)
            {
                await UniTask.SwitchToMainThread();
            }
            //do not log everything in release build
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (logType == LogType.Log) 
            {
                Debug.Log($"{Tag}/{_prefix}: " + message);
            }
            else if (logType == LogType.Error) 
            {
                Debug.LogError($"{Tag}/{_prefix}: " + message);
            }
#endif
        }
    }
}
