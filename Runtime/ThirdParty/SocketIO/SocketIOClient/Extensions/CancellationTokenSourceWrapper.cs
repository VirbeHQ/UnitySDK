using System;
using System.Threading;

namespace SocketIOClient.Extensions
{
    class CancellationTokenSourceWrapper: IDisposable
    {
        private readonly CancellationTokenSource _cts;
        private bool _cancelled;
        private bool _disposed;

        public CancellationTokenSourceWrapper(CancellationTokenSource cts)
        {
            _cts = cts ?? throw new ArgumentNullException(nameof(cts));
        }

        private void Cancel()
        {
            if (_cancelled)
            {
                return;
            }
            _cts.Cancel();
            _cancelled = true;
        }

        private void DisposeInternal()
        {
            if (_disposed)
            {
                return;
            }
            _cts.Dispose();
            _disposed = true;
        }

        public CancellationToken Token => _cts.Token;

        public bool IsCancellationRequested => _cts.IsCancellationRequested;

        public void Dispose()
        {
            Cancel();
            DisposeInternal();
        }
    }
}