// <copyright file="MetricUnixDataTransport.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Geneva
{
    internal sealed class MetricUnixDataTransport : IMetricDataTransport
    {
        private readonly int fixedPayloadLength;
        private readonly UnixDomainSocketDataTransport udsDataTransport;
        private bool isDisposed;

        public MetricUnixDataTransport(
            string unixDomainSocketPath,
            int timeoutMilliseconds = UnixDomainSocketDataTransport.DefaultTimeoutMilliseconds)
        {
            unsafe
            {
                this.fixedPayloadLength = sizeof(BinaryHeader);
            }

            this.udsDataTransport = new UnixDomainSocketDataTransport(unixDomainSocketPath, timeoutMilliseconds);
        }

        public void Send(MetricEventType eventType, byte[] body, int size)
        {
            this.udsDataTransport.Send(body, size + this.fixedPayloadLength);
        }

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.udsDataTransport?.Dispose();
            this.isDisposed = true;
        }
    }
}
