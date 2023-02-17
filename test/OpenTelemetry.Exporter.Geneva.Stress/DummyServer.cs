// <copyright file="DummyServer.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;

namespace OpenTelemetry.Exporter.Geneva.Stress;

internal class DummyServer
{
    private EndPoint endpoint;
    private Socket serverSocket;

    public DummyServer(string path)
    {
        Console.WriteLine($"Server socket listening at path: {path}");

        // Unix sockets must be unlink()ed before being reused again.
        // Or there will be System.Net.Sockets.SocketException (98): SocketError.AddressAlreadyInUse
        // https://github.com/dotnet/runtime/issues/23803
        // C# doesn't have the unlink() function in C
        // Shutdown() and setting SocketOptions like ReuseAddress and Linger doesn't solve the problem as they do for TCP
        // https://stackoverflow.com/questions/2821520/how-can-i-unbind-a-socket-in-c
        File.Delete(path);
        this.endpoint = new UnixDomainSocketEndPoint(path);
        this.serverSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
    }

    public void Start()
    {
        try
        {
            this.serverSocket.Bind(this.endpoint);
            this.serverSocket.Listen(20);

            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs args) =>
            {
                Console.WriteLine("Program is terminating.");
                this.serverSocket.Close();
            };

            while (true)
            {
                Socket acceptSocket = this.serverSocket.Accept();
                Task.Run(() =>
                {
                    int threadId = Environment.CurrentManagedThreadId;
                    Console.WriteLine($"ThreadID {threadId}: Start reading from socket.");
                    int totalBytes = 0;
                    try
                    {
                        while (acceptSocket.Connected)
                        {
                            var receivedData = new byte[1024];
                            int receivedDataSize = acceptSocket.Receive(receivedData);
                            totalBytes += receivedDataSize;
                        }

                        acceptSocket.Shutdown(SocketShutdown.Both);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"acceptSocket exception: {e}");
                    }
                    finally
                    {
                        Console.WriteLine($"ThreadID {threadId}: Closing socket");
                        acceptSocket.Close();
                    }

                    Console.WriteLine($"ThreadID {threadId}: Socket received {totalBytes} bytes in total.");
                });
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Server socket exception: {e}");
        }
        finally
        {
            this.serverSocket.Close();
        }
    }
}
