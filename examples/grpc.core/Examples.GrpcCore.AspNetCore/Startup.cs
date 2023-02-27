// <copyright file="Startup.cs" company="OpenTelemetry Authors">
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

using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Instrumentation.GrpcCore;
using OpenTelemetry.Trace;

namespace Examples.GrpcCore.AspNetCore;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        this.Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();

        // Wire in otel
        services.AddOpenTelemetry().WithTracing(
            builder => builder
                .AddAspNetCoreInstrumentation()
                .AddGrpcCoreInstrumentation()
                .AddConsoleExporter());

        // We are running an in-process gRPC Core service.
        services.AddHostedService<EchoGrpcHostedService>();

        // Add a singleton for the gRPC client to our local service.
        services.AddSingleton(provider =>
        {
            var channel = new Channel($"dns:localhost:{Program.GrpcServicePort}", ChannelCredentials.Insecure);

            var callInvoker = channel.CreateCallInvoker()
                .Intercept(new ClientTracingInterceptor(new ClientTracingInterceptorOptions()));

            return new Echo.EchoClient(callInvoker);
        });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();

        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }

    /// <summary>
    /// A hosted service wrapper for an in-process gRPC Core service.
    /// This gRPC service is instrumented using the server interceptor.
    /// </summary>
    private sealed class EchoGrpcHostedService : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var serviceDefinition = Echo.BindService(new EchoService())
                .Intercept(new ServerTracingInterceptor(new ServerTracingInterceptorOptions()));

            var server = new Server
            {
                Ports = { new ServerPort("localhost", Program.GrpcServicePort, ServerCredentials.Insecure) },
                Services = { serviceDefinition },
            };

            server.Start();

            var tcs = new TaskCompletionSource<bool>();

            var tokenRegistration = stoppingToken.Register(
                async () =>
                {
                    await server.ShutdownAsync().ConfigureAwait(false);
                    tcs.SetResult(true);
                });

            return tcs.Task.ContinueWith(antecedent => tokenRegistration.Dispose(), stoppingToken);
        }
    }
}
