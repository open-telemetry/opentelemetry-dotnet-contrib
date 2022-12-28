// <copyright file="UnixDomainSocketDataTransport.cs" company="OpenTelemetry Authors">
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

internal class UnixDomainSocketDataTransport : IDataTransport, IDisposable
{
    private readonly EndPoint unixEndpoint;
    private Socket socket;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnixDomainSocketDataTransport"/> class.
    /// The class for transporting data over Unix domain socket.
    /// </summary>
    /// <param name="unixDomainSocketPath">The path to connect a unix domain socket over.</param>
    /// <param name="timeoutMilliseconds">Timeout in milliseconds. Defaults to <see cref="TransportDefaults.SocketTimeoutMilliseconds" />.</param>
    public UnixDomainSocketDataTransport(string unixDomainSocketPath, int timeoutMilliseconds = TransportDefaults.SocketTimeoutMilliseconds)
    {
        this.unixEndpoint = new UnixDomainSocketEndPoint(unixDomainSocketPath);
        this.socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP)
        {
            SendTimeout = timeoutMilliseconds
        };

        this.Connect();
    }

    public bool SupportsBatching => true;

    public bool IsEnabled()
    {
        return true;
    }

    public void Send(byte[] data, int size)
    {
        try
        {
            if (!this.socket.Connected)
            {
                // Socket connection is off! Server might have stopped. Trying to reconnect.
                this.Reconnect();
            }

            this.socket.Send(data, size, SocketFlags.None);
        }
        catch (Exception)
        {
            // Re-throw the exception so that Export method catches it and sets the ExportResult correctly.
            throw;
        }
    }

    public void Dispose()
    {
        this.socket.Dispose();
    }

    private void Connect()
    {
        try
        {
            this.socket.Connect(this.unixEndpoint);
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException("UDS Connect failed.", ex);

            // Re-throw the exception to
            // 1. fail fast in Geneva exporter contructor, or
            // 2. fail in the Reconnect attempt.
            throw;
        }
    }

    private void Reconnect()
    {
        this.socket.Close();
        this.Connect();
    }
}
