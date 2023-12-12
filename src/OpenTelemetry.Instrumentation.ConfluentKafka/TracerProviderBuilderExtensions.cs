// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Instrumentation.ConfluentKafka;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static partial class TracerProviderBuilderExtensions
{
    private static TracerProviderBuilder AddKafkaInstrumentationSharedServices(
    this TracerProviderBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        return builder.ConfigureServices(services =>
        {
            services.TryAddSingleton<ConfluentKafkaInstrumentation>();
            services.TryAddSingleton<MetricsChannel>();
            services.TryAddSingleton<ConfluentKafkaMeterInstrumentation>();
        });
    }
}
