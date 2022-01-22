// <copyright file="Program.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddOpenTelemetryMetrics(
                (builder) => builder
                    .AddPrometheusExporter()
                    .AddEventCounters(options =>
                    {
                        options.AddRuntime("cpu-usage");
                        options.AddAspNetCore();
                        options.AddProvider("Microsoft-AspNetCore-Server-Kestrel", "total-connections");
                        options.MetricNameMapper = (eventSource, countername) =>
                        {
                            if (countername == "total-requests")
                            {
                                return "http_requests_received_total";
                            }
                            else
                            {
                                return null;
                            }
                        };
                    }));

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.Run();
