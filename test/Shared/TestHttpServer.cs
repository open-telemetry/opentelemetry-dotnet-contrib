// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// Note: When implicit usings are enabled in a project this file will generate
// warnings/errors without this suppression.

using System.Net;
#if !NETFRAMEWORK
using System.Security.Cryptography;
#endif

namespace OpenTelemetry.Tests;

internal static class TestHttpServer
{
#if !NET
    private static readonly Random GlobalRandom = new();
#endif

    public static IDisposable RunServer(Action<HttpListenerContext> action, out string host, out int port)
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
                server = new RunningServer(action, host, port);
                server.Start();
                break;
            }
            catch (HttpListenerException ex)
            {
                server?.Dispose();
                server = null;
                if (--retryCount <= 0)
                {
                    throw new InvalidOperationException("TestHttpServer could not be started.", ex);
                }
            }
        }

        return server;
    }

    private sealed class RunningServer : IDisposable
    {
        private readonly Task httpListenerTask;
        private readonly HttpListener listener;
        private readonly TaskCompletionSource<bool> initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RunningServer(Action<HttpListenerContext> action, string host, int port)
        {
            this.listener = new HttpListener();

            this.listener.Prefixes.Add($"http://{host}:{port}/");
            this.listener.Start();

            this.httpListenerTask = Task.Run(() => this.ListenAsync(action));
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
                this.httpListenerTask.GetAwaiter().GetResult();
            }
            catch (Exception ex) when (this.IsListenerShutdownException(ex))
            {
                // Listener was already closed as part of disposal.
            }
        }

        private bool IsListenerShutdownException(Exception ex)
        {
            return ex is ObjectDisposedException
                || (ex is HttpListenerException httpEx && (httpEx.ErrorCode == 995 || httpEx.ErrorCode == 6))
                || (ex is InvalidOperationException && !this.listener.IsListening);
        }

        private async Task ListenAsync(Action<HttpListenerContext> action)
        {
            this.initialized.TrySetResult(true);

            while (true)
            {
                try
                {
                    var context = await this.listener.GetContextAsync().ConfigureAwait(false);
                    action(context);
                }
                catch (Exception ex)
                {
                    if (this.IsListenerShutdownException(ex))
                    {
                        // Listener was closed before we got into GetContextAsync or
                        // Listener was closed while we were in GetContextAsync.
                        break;
                    }

                    throw;
                }
            }
        }
    }
}
