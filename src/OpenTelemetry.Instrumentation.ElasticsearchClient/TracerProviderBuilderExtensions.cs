// <copyright file="TracerProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.ElasticsearchClient;
using OpenTelemetry.Instrumentation.ElasticsearchClient.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Enables Elasticsearch client Instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddElasticsearchClientInstrumentation(
        this TracerProviderBuilder builder)
    {
        return AddElasticsearchClientInstrumentation(builder, name: null, configure: null);
    }

    /// <summary>
    /// Enables Elasticsearch client Instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="configure">Elasticsearch client configuration options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddElasticsearchClientInstrumentation(
        this TracerProviderBuilder builder,
        Action<ElasticsearchClientInstrumentationOptions> configure)
    {
        return AddElasticsearchClientInstrumentation(builder, name: null, configure);
    }

    /// <summary>
    /// Enables Elasticsearch client Instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configure">Elasticsearch client configuration options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddElasticsearchClientInstrumentation(
        this TracerProviderBuilder builder,
        string name,
        Action<ElasticsearchClientInstrumentationOptions> configure)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        if (configure != null)
        {
            builder.ConfigureServices(services => services.Configure(name, configure));
        }

        builder.AddInstrumentation(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<ElasticsearchClientInstrumentationOptions>>().Get(name);
            return new ElasticsearchClientInstrumentation(options);
        });

        builder.AddSource(ElasticsearchRequestPipelineDiagnosticListener.ActivitySourceName);
        builder.AddLegacySource("CallElasticsearch");

        return builder;
    }
}
