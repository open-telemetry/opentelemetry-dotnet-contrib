// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Instrumentation.ConfluentKafka;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of Kafka instrumentation.
/// </summary>
public static partial class MeterProviderBuilderExtensions
{
    private static MeterProviderBuilder AddKafkaInstrumentationSharedServices(this MeterProviderBuilder builder)
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
