// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal class ConfluentKafkaCommon
{
    internal static readonly string ActivitySourceName = typeof(ConfluentKafkaInstrumentation).Assembly.GetName().Name!;

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
