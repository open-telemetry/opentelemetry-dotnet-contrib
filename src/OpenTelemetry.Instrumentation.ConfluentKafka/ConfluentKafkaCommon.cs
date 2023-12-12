// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal static class ConfluentKafkaCommon
{
    internal static readonly string InstrumentationName = typeof(ConfluentKafkaCommon).Assembly.GetName().Name!;
    internal static readonly string InstrumentationVersion = typeof(ConfluentKafkaCommon).Assembly.GetPackageVersion();
    internal static readonly ActivitySource ActivitySource = new(InstrumentationName, InstrumentationVersion);
}
