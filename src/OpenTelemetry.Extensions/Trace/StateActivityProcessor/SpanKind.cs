// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
namespace OpenTelemetry.Extensions.Trace.StateActivityProcessor;

public enum SpanKind
{
    SpanKindUnspecified = 0,
    SpanKindInternal = 1,
    SpanKindServer = 2,
    SpanKindClient = 3,
    SpanKindProducer = 4,
    SpanKindConsumer = 5
}
