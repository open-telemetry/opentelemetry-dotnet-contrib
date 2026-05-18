// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;

namespace OpenTelemetry.Tests;

internal static class TestHttpServer
{
    public static IDisposable RunServer(Action<HttpListenerContext> action, out Uri baseAddress)
    {
        var uri = new UriBuilder()
        {
            Host = "localhost",
            Scheme = Uri.UriSchemeHttp,
        };

        RunningServer? server = null;

        var retryCount = 5;
        while (true)
        {
            try
            {
                uri.Port = TcpPortProvider.GetOpenPort();
                server = new RunningServer(action, uri.Uri);
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

        baseAddress = uri.Uri;

        return server;
    }

    private sealed class RunningServer : IDisposable
    {
        private readonly Task httpListenerTask;
        private readonly HttpListener listener;
        private readonly TaskCompletionSource<bool> initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource cancellationTokenSource = new();

        public RunningServer(Action<HttpListenerContext> action, Uri baseAddress)
        {
            this.listener = new HttpListener();

            this.listener.Prefixes.Add(baseAddress.ToString());
            this.listener.Start();

            this.httpListenerTask = Task.Factory.StartNew(
                () => this.ListenAsync(action),
                this.cancellationTokenSource.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();
        }

        public void Start() => this.initialized.Task.GetAwaiter().GetResult();

        public void Dispose()
        {
            try
            {
                this.cancellationTokenSource.Cancel();
                this.cancellationTokenSource.Dispose();
                this.listener.Close();
                this.httpListenerTask.Wait();
            }
            catch (Exception ex) when (this.IsListenerShutdownException(ex))
            {
                // Listener was already closed as part of disposal.
            }
        }

        private static bool IsResponseAlreadyClosedException(Exception exception)
        {
            for (var ex = exception; ex is not null; ex = ex.InnerException)
            {
                if (ex is AggregateException aggregate)
                {
                    ex = aggregate.Flatten();
                }

                if (ex is ObjectDisposedException)
                {
                    return true;
                }

                if (ex is HttpListenerException httpEx && (httpEx.ErrorCode is 1 or 6 or 995 or 1229 or 10057))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsListenerShutdownException(Exception exception)
        {
            if (IsResponseAlreadyClosedException(exception))
            {
                return true;
            }

            for (var ex = exception; ex is not null; ex = ex.InnerException)
            {
                if (ex is AggregateException aggregate)
                {
                    ex = aggregate.Flatten();
                }

                if (ex is InvalidOperationException && !this.listener.IsListening)
                {
                    return true;
                }
            }

            return false;
        }

        private async Task ListenAsync(Action<HttpListenerContext> action)
        {
            this.initialized.TrySetResult(true);

            while (!this.cancellationTokenSource.IsCancellationRequested)
            {
                HttpListenerContext? context = null;

                try
                {
                    context = await this.listener.GetContextAsync().ConfigureAwait(false);

                    try
                    {
                        action(context);
                    }
                    catch (Exception ex) when (IsResponseAlreadyClosedException(ex))
                    {
                        // Client disconnected / response stream already torn down while the handler
                        // was writing the response or disposing response writer/stream.
                        // Treat as non-fatal and continue accepting new requests.
                    }
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
                finally
                {
                    if (context is not null)
                    {
                        this.TryCloseResponse(context.Response);
                    }
                }

                await Task.Yield();
            }
        }

        private void TryCloseResponse(HttpListenerResponse response)
        {
            try
            {
                response.Close();
            }
            catch (Exception ex) when (IsResponseAlreadyClosedException(ex))
            {
                // The handler completed the response explicitly or the client disconnected.
            }
        }
    }
}
