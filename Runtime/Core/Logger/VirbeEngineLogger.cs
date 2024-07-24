using UnityEngine;

// ReSharper disable UnusedMember.Global

namespace Virbe.Core.Logger
{
    internal class VirbeEngineLogger
    {
        private static readonly ILogger Logger = Debug.unityLogger;

        private const string Tag = "VirbeEngine";

        private readonly string _prefix;

        public VirbeEngineLogger(string prefix)
        {
            _prefix = prefix;
        }

        internal void Log(string format, params object[] args)
        {
            Logger.LogFormat(LogType.Log, $"{Tag}/{_prefix}: " + format, args);
        }

        internal void LogError(string format, params object[] args)
        {
            Logger.LogFormat(LogType.Error, $"{Tag}/{_prefix}: " + format, args);
        }
    }
}