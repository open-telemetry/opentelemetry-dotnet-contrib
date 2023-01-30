// <copyright file="TcpSocketDataTransportTests.cs" company="OpenTelemetry Authors">
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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class TcpSocketDataTransportTests
{
    private const string TestHost = "localhost";
    private const int TestPort = 12023;

    [Fact]
    public void TcpSocketDataTransport_Success()
    {
        var endpoint = new IPEndPoint(IPAddress.Any, TestPort);
        using var server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        server.Bind(endpoint);
        server.Listen(1);

        // Client
        using var dataTransport = new TcpSocketDataTransport(TestHost, TestPort);
        using Socket serverSocket = server.Accept();
        var data = new byte[] { 12, 34, 56 };
        dataTransport.Send(data, data.Length);
        var receivedData = new byte[3];
        serverSocket.Receive(receivedData);
        Assert.Equal(data[0], receivedData[0]);
        Assert.Equal(data[1], receivedData[1]);
        Assert.Equal(data[2], receivedData[2]);
    }

    [Fact(Skip = "Test does not perform server tear down correctly")]
    public void TcpSocketDataTransport_ServerRestart()
    {
        var endpoint = new IPEndPoint(IPAddress.Any, TestPort);
        var server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        LingerOption lo = new LingerOption(false, 0);
        server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, lo);

        try
        {
            server.Bind(endpoint);
            server.Listen(1);

            // Client
            using var dataTransport = new TcpSocketDataTransport(TestHost, TestPort);
            Socket serverSocket = server.Accept();
            var data = new byte[] { 12, 34, 56 };
            dataTransport.Send(data, data.Length);
            var receivedData = new byte[3];
            serverSocket.Receive(receivedData);
            Assert.Equal(data[0], receivedData[0]);
            Assert.Equal(data[1], receivedData[1]);
            Assert.Equal(data[2], receivedData[2]);

            // Emulate server stops
            serverSocket.Shutdown(SocketShutdown.Both);
            serverSocket.Disconnect(false);
            serverSocket.Dispose();
            server.Shutdown(SocketShutdown.Both);
            server.Disconnect(false);

            Assert.ThrowsAny<Exception>(() => dataTransport.Send(data, data.Length));
            Assert.ThrowsAny<Exception>(() => dataTransport.Send(data, data.Length));

            using var server2 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                server2.Bind(endpoint);
                server2.Listen(1);

                var data2 = new byte[] { 34, 56, 78 };
                dataTransport.Send(data2, data2.Length);

                using Socket serverSocket2 = server2.Accept();
                var receivedData2 = new byte[5];
                serverSocket2.Receive(receivedData2);
                Assert.Equal(data2[0], receivedData2[0]);
                Assert.Equal(data2[1], receivedData2[1]);
                Assert.Equal(data2[2], receivedData2[2]);
            }
            finally
            {
                server2.Dispose();
            }
        }
        finally
        {
            server.Dispose();
        }
    }
}
