// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;
using OpenTelemetry.Instrumentation.ConfluentKafka;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static partial class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Enables Tracing of Processing Consumed messages when using the extensions on <see cref="IConsumer{TKey,TValue}"/>.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKafkaProcessorInstrumentation(
        this TracerProviderBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        return builder
            .AddSource(ConfluentKafkaCommon.ProcessorActivitySource.Name);
    }
}
