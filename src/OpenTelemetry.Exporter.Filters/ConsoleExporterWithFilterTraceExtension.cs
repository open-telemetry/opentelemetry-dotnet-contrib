// <copyright file="ConsoleExporterWithFilterTraceExtension.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.Filters;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Console Exporter Extension with Filter or Sampler as parameters.
/// </summary>
public static class ConsoleExporterWithFilterTraceExtension
{
    /// <summary>
    /// Adds Console exporter to the TracerProvider.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="filter"><see cref="BaseFilter&lt;Activity&gt;"/> filter to use.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddConsoleExporter(this TracerProviderBuilder builder, BaseFilter<Activity> filter)
        => AddConsoleExporter(builder, name: null, configure: null, filter: filter);

    /// <summary>
    /// Adds Console exporter to the TracerProvider.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="sampler"><see cref="Sampler"/> sampler to use.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddConsoleExporter(this TracerProviderBuilder builder, Sampler sampler)
        => AddConsoleExporter(builder, name: null, configure: null, filter: new SamplerFilter(sampler));

    /// <summary>
    /// Adds Console exporter to the TracerProvider.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="configure">Callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
    /// <param name="filter"><see cref="BaseFilter&lt;Activity&gt;"/> filter to use.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddConsoleExporter(this TracerProviderBuilder builder, Action<ConsoleExporterOptions> configure, BaseFilter<Activity> filter)
        => AddConsoleExporter(builder, name: null, configure, filter: filter);

    /// <summary>
    /// Adds Console exporter to the TracerProvider.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="configure">Callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
    /// <param name="sampler"><see cref="Sampler"/> sampler to use.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddConsoleExporter(this TracerProviderBuilder builder, Action<ConsoleExporterOptions> configure, Sampler sampler)
        => AddConsoleExporter(builder, name: null, configure, filter: new SamplerFilter(sampler));

    /// <summary>
    /// Adds Console exporter to the TracerProvider.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configure">Callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
    /// <param name="sampler"><see cref="BaseFilter&lt;Activity&gt;"/> filter to use.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddConsoleExporter(
        this TracerProviderBuilder builder,
        string name,
        Action<ConsoleExporterOptions> configure,
        Sampler sampler)
    => AddConsoleExporter(builder, name: name, configure, filter: new SamplerFilter(sampler));

    /// <summary>
    /// Adds Console exporter to the TracerProvider.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configure">Callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
    /// <param name="filter"><see cref="BaseFilter&lt;Activity&gt;"/> filter to use.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddConsoleExporter(
        this TracerProviderBuilder builder,
        string name,
        Action<ConsoleExporterOptions> configure,
        BaseFilter<Activity> filter)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        if (configure != null)
        {
            builder.ConfigureServices(services => services.Configure(name, configure));
        }

        var options = new ConsoleExporterOptions();
        configure?.Invoke(options);
        return builder.AddProcessor(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<ConsoleExporterOptions>>().Get(name);

            return new SimpleActivityExportProcessorWithFilter(new ConsoleActivityExporter(options), filter);
        });
    }
}
