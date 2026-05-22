// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Instrumentation.Grpc.Tests;

internal sealed class GrpcServer<TService> : IAsyncDisposable
    where TService : class
{
    private IHost? host;

    public Uri Address
    {
        get => field ?? throw new InvalidOperationException("Server has not been started.");
        private set;
    }

    public async Task StartAsync()
    {
        if (this.host != null)
        {
            throw new InvalidOperationException("Server is already started.");
        }

        var retryCount = 5;
        var attemptsRemaining = retryCount;

        while (attemptsRemaining > 0)
        {
            try
            {
                var uri = new UriBuilder()
                {
                    Host = "localhost",
                    Port = TcpPortProvider.GetOpenPort(),
                    Scheme = Uri.UriSchemeHttp,
                }.Uri;

                var host = this.CreateServer(uri.Port);

                try
                {
                    await host.StartAsync();

                    this.host = host;
                    this.Address = uri;

                    return;
                }
                catch (Exception)
                {
                    host.Dispose();
                    throw;
                }
            }
            catch (IOException)
            {
                attemptsRemaining--;
                this.host?.Dispose();
            }
        }

        throw new InvalidOperationException($"Failed to start server within {retryCount} attempts.");
    }

    public async ValueTask DisposeAsync()
    {
        if (this.host != null)
        {
            await this.host.StopAsync(TimeSpan.FromSeconds(5));
        }

        this.host?.Dispose();
        GC.SuppressFinalize(this);
    }

    private IHost CreateServer(int port)
    {
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                // Setup a HTTP/2 endpoint without TLS
                webBuilder
                    .ConfigureKestrel(options => options.ListenLocalhost(port, o => o.Protocols = HttpProtocols.Http2))
                    .UseStartup<Startup>();
            });

        return hostBuilder.Build();
    }

    private class Startup
    {
        public void ConfigureServices(IServiceCollection services) => services.AddGrpc();

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<TService>();
            });
        }
    }
}
#endif
