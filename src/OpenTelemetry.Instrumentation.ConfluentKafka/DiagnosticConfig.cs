// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

/// <summary>
/// Diagnositc names for the Kafka instrumentation.
/// </summary>
public static class DiagnosticConfig
{
    /// <summary>
    /// The name of the ActivitySource for the Kafka Consumer to listen to.
    /// </summary>
    public static readonly string ConsumerActivitySourceName = ConfluentKafkaCommon.ConsumerActivitySource.Name;

    /// <summary>
    /// The name of the ActivitySource for the Kafka Producer to listen to.
    /// </summary>
    public static readonly string ProducerActivitySourceName = ConfluentKafkaCommon.ProducerActivitySource.Name;

}
