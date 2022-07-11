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
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace OpenTelemetry.Exporter.Geneva;

internal class TcpDataTransport : IDataTransport, IDisposable
{
    public const int DefaultTimeoutMilliseconds = 15000;
    private readonly string host;
    private readonly int port;
    private Socket socket;
    private int timeoutMilliseconds;

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpDataTransport"/> class.
    /// The class for transporting data over Unix domain socket.
    /// </summary>
    /// <param name="host">The host name to connect to.</param>
    /// <param name="port">The port to connect to.</param>
    /// <param name="timeoutMilliseconds">
    /// The time-out value, in milliseconds.
    /// If you set the property with a value between 1 and 499, the value will be changed to 500.
    /// The default value is 15,000 milliseconds.
    /// </param>
    public TcpDataTransport(
        string host,
        int port,
        int timeoutMilliseconds = DefaultTimeoutMilliseconds)
    {
        this.host = host;
        this.port = port;
        this.timeoutMilliseconds = timeoutMilliseconds;
        this.Connect();
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
        catch (SocketException ex)
        {
            // SocketException from Socket.Send
            ExporterEventSource.Log.ExporterException(ex);
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException(ex);
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
            // Resolve host name here, rather than in constructor, so that if we have to reconnect we re-resolve (eg. so dns can return us a different ip address).
            var hostEntry = Dns.GetHostEntry(this.host);
            var ipAddress = hostEntry.AddressList.First();
            var ipEndPoint = new IPEndPoint(ipAddress, this.port);

            this.socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                SendTimeout = this.timeoutMilliseconds,
            };
            this.socket.Connect(ipEndPoint);
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException(ex);

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
