// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.WebSockets;
#if !NETFRAMEWORK
using System.Security.Cryptography;
#endif

namespace OpenTelemetry.Tests;

internal static class TestWebSocketServer
{
#if !NET
    private static readonly Random GlobalRandom = new();
#endif

    public static IDisposable RunServer(Func<WebSocket, Task> handler, out string host, out int port)
    {
        return RunServer((_, socket) => handler(socket), out host, out port);
    }

    public static IDisposable RunServer(Func<HttpListenerContext, WebSocket, Task> handler, out string host, out int port)
    {
        host = "localhost";
        port = 0;
        RunningServer? server = null;

        var retryCount = 5;
        while (true)
        {
            try
            {
#if NET
                port = RandomNumberGenerator.GetInt32(2000, 5000);
#else
#pragma warning disable CA5394 // Do not use insecure randomness
                port = GlobalRandom.Next(2000, 5000);
#pragma warning restore CA5394 // Do not use insecure randomness
#endif
                server = new RunningServer(handler, host, port);
                server.Start();
                break;
            }
            catch (HttpListenerException ex)
            {
                server?.Dispose();
                server = null;
                if (--retryCount <= 0)
                {
                    throw new InvalidOperationException("TestWebSocketServer could not be started.", ex);
                }
            }
        }

        return server;
    }

    private sealed class RunningServer : IDisposable
    {
        private readonly Task listenerTask;
        private readonly HttpListener listener;
        private readonly TaskCompletionSource<bool> initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RunningServer(Func<HttpListenerContext, WebSocket, Task> handler, string host, int port)
        {
            this.listener = new HttpListener();
            this.listener.Prefixes.Add($"http://{host}:{port}/");
            this.listener.Start();

            this.listenerTask = Task.Run(() => this.ListenAsync(handler));
        }

        public void Start()
        {
            this.initialized.Task.GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            try
            {
                this.listener.Close();
                this.listenerTask.GetAwaiter().GetResult();
            }
            catch (Exception ex) when (this.IsListenerShutdownException(ex))
            {
                // Listener was already closed as part of disposal.
            }
        }

        private bool IsListenerShutdownException(Exception ex)
        {
            // Win32 error codes surfaced by HttpListener when the listener is closed while
            // GetContextAsync is pending:
            //   995 ERROR_OPERATION_ABORTED - normal abort when the listener is stopped
            //   6   ERROR_INVALID_HANDLE    - listener handle was already closed
            //   1   ERROR_INVALID_FUNCTION  - .NET Framework raises this instead of 995 for WebSocket
            //                                 contexts; guarded by !IsListening to avoid swallowing a
            //                                 genuine failure that happens to surface as code 1
            return ex is ObjectDisposedException
                || (ex is HttpListenerException httpEx
                    && (httpEx.ErrorCode == 995
                        || httpEx.ErrorCode == 6
                        || (httpEx.ErrorCode == 1 && !this.listener.IsListening)))
                || (ex is InvalidOperationException && !this.listener.IsListening);
        }

        private async Task ListenAsync(Func<HttpListenerContext, WebSocket, Task> handler)
        {
            this.initialized.TrySetResult(true);

            while (true)
            {
                try
                {
                    var ctx = await this.listener.GetContextAsync().ConfigureAwait(false);

                    if (ctx.Request.IsWebSocketRequest)
                    {
                        var wsContext = await ctx.AcceptWebSocketAsync(null).ConfigureAwait(false);
                        await handler(ctx, wsContext.WebSocket).ConfigureAwait(false);
                    }
                    else
                    {
                        ctx.Response.StatusCode = 400;
                        ctx.Response.Close();
                    }
                }
                catch (Exception ex)
                {
                    if (this.IsListenerShutdownException(ex))
                    {
                        break;
                    }

                    throw;
                }
            }
        }
    }
}
