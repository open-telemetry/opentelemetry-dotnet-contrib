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

using System;
using System.Threading.Tasks;
using Examples.ElasticSearch.Model;
using Nest;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Examples.Elasticsearch
{
    internal static class Program
    {
        public static async Task Main()
        {
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ElasticSearch"))
                .AddElasticsearchClientInstrumentation(options =>
                {
                    options.ParseAndFormatRequest = true;
                    options.SuppressDownstreamInstrumentation = true;
                    options.SetDbStatementForRequest = true;
                    options.Enrich = (activity, eventName, rawObject) =>
                    {
                        activity.IsAllDataRequested = true;
                    };
                })
                .AddJaegerExporter()
                .Build();

            var node = new Uri("http://localhost:9200");
            var settings = new ConnectionSettings(node);
            settings.DisableDirectStreaming();
            var client = new ElasticClient(settings);

            var forecast = new WeatherForecast
            {
                Id = 1,
                Date = DateTime.Now,
                TemperatureCelsius = 25,
                Summary = "25ºC preview for Portugal",
            };

            await client.IndexAsync(forecast, idx => idx.Index("forecast-index"));

            await client.GetAsync<WeatherForecast>(1, idx => idx.Index("forecast-index"));
        }
    }
}
