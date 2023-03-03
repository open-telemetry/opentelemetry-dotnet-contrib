// <copyright file="CallbackManager.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(CallbackManager<T>));
            }

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
