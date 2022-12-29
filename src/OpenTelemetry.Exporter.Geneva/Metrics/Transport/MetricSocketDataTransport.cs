// <copyright file="MetricSocketDataTransport.cs" company="OpenTelemetry Authors">
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

using System;

namespace OpenTelemetry.Exporter.Geneva;

internal sealed class MetricSocketDataTransport : IMetricDataTransport
{
    private readonly int fixedPayloadLength;
    private readonly IDataTransport socketDataTransport;
    private bool isDisposed;

    public MetricSocketDataTransport(IDataTransport socketDataTransport)
    {
        unsafe
        {
            this.fixedPayloadLength = sizeof(BinaryHeader);
        }

        this.socketDataTransport = socketDataTransport ?? throw new ArgumentNullException(nameof(socketDataTransport));
    }

    public void Send(MetricEventType eventType, byte[] body, int size)
    {
        this.socketDataTransport.Send(body, size + this.fixedPayloadLength);
    }

    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        if (this.socketDataTransport is IDisposable disposableDataTransport)
        {
            disposableDataTransport.Dispose();
        }

        this.isDisposed = true;
    }
}
