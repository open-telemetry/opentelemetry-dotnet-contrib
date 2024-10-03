// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Exporter.OneCollector;

internal sealed class CallbackManager<T> : IDisposable
    where T : Delegate
{
    private readonly object lockObject = new();
    private T? root;
    private bool disposed;

    public T? Root { get => this.root; }

    public IDisposable Add(T callback)
    {
        Debug.Assert(callback != null, "callback was null");

        lock (this.lockObject)
        {
#if NET
            ObjectDisposedException.ThrowIf(this.disposed, nameof(CallbackManager<T>));
#else
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(CallbackManager<T>));
            }
#endif

            this.root = (T)Delegate.Combine(this.root, callback);
        }

        return new CallbackManagerRegistration(() =>
        {
            lock (this.lockObject)
            {
                this.root = (T?)Delegate.Remove(this.root, callback);
            }
        });
    }

    public void Dispose()
    {
        lock (this.lockObject)
        {
            this.root = null;
            this.disposed = true;
        }
    }

    private sealed class CallbackManagerRegistration : IDisposable
    {
        private readonly Action disposeAction;

        public CallbackManagerRegistration(Action disposeAction)
        {
            Debug.Assert(disposeAction != null, "disposeAction was null");

            this.disposeAction = disposeAction!;
        }

        public void Dispose()
        {
            this.disposeAction();
        }
    }
}
