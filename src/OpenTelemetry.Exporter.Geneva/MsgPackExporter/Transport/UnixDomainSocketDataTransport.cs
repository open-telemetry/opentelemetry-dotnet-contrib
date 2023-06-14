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

internal sealed class UnixDomainSocketDataTransport : IDataTransport, IDisposable
{
    public const int DefaultTimeoutMilliseconds = 15000;
    private readonly EndPoint unixEndpoint;
    private Socket socket;
    private int timeoutMilliseconds;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnixDomainSocketDataTransport"/> class.
    /// The class for transporting data over Unix domain socket.
    /// </summary>
    /// <param name="unixDomainSocketPath">The path to connect a unix domain socket over.</param>
    /// <param name="timeoutMilliseconds">
    /// The time-out value, in milliseconds.
    /// If you set the property with a value between 1 and 499, the value will be changed to 500.
    /// The default value is 15,000 milliseconds.
    /// </param>
    public UnixDomainSocketDataTransport(
        string unixDomainSocketPath,
        int timeoutMilliseconds = DefaultTimeoutMilliseconds)
    {
        this.unixEndpoint = new UnixDomainSocketEndPoint(unixDomainSocketPath);
        this.timeoutMilliseconds = timeoutMilliseconds;
        try
        {
            this.Connect();
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException("UDS unavailable at startup.", ex);
        }
    }

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
            this.socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP)
            {
                SendTimeout = this.timeoutMilliseconds,
            };
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
