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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;

namespace Examples.EventCounter.AspNetCore.Controllers
{
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
            services.AddOpenTelemetryMetrics(
                (builder) => builder
                .AddPrometheusExporter()
                .AddEventCounters(options =>
                {
                    options.AddRuntime()
                .WithCounters("cpu-usage", "working-set");

                    options.AddAspNetCore()
                .WithCurrentRequests("http_requests_in_progress")
                .WithFailedRequests()
                .WithRequestRate()
                .WithTotalRequests("http_requests_received_total");

                    options.AddEventSource("Microsoft-AspNetCore-Server-Kestrel")
                .WithCounters("total-connections")
                .With("connections-per-second", "The number of connections per update interval to the web server", MetricType.LongSum);

                    options.AddEventSource("Microsoft.AspNetCore.Http.Connections");
                }));
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

            app.UseHttpsRedirection();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseOpenTelemetryPrometheusScrapingEndpoint();
        }
    }
}
