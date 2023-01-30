// <copyright file="UdpSocketDataTransport.cs" company="OpenTelemetry Authors">
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
using System.Net;
using System.Net.Sockets;

namespace OpenTelemetry.Exporter.Geneva;

internal class UdpSocketDataTransport : IDataTransport, IDisposable
{
    private readonly EndPoint networkEndpoint;
    private readonly int timeoutMilliseconds;

    private Socket socket;

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpSocketDataTransport"/> class.
    /// The class for transporting data over the UDP network socket.
    /// </summary>
    /// <param name="host">Host name of the UDP endpoint.</param>
    /// <param name="port">Port of the UDP endpoint.</param>
    /// <param name="timeoutMilliseconds">Timeout in milliseconds. Defaults to <see cref="TransportDefaults.SocketTimeoutMilliseconds" />.</param>
    public UdpSocketDataTransport(string host, int port, int timeoutMilliseconds = TransportDefaults.SocketTimeoutMilliseconds)
    {
        this.networkEndpoint = new DnsEndPoint(host, port);
        this.timeoutMilliseconds = timeoutMilliseconds;

        this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            SendTimeout = this.timeoutMilliseconds,
        };
    }

    public bool SupportsBatching => false;

    public bool IsEnabled()
    {
        return true;
    }

    public void Send(byte[] data, int size)
    {
        this.socket.SendTo(data, size, SocketFlags.None, this.networkEndpoint);
    }

    public void Dispose()
    {
        this.socket.Dispose();
    }
}
