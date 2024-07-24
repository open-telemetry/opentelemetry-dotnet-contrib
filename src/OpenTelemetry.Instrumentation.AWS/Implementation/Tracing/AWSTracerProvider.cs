// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Runtime.Telemetry.Tracing;

namespace OpenTelemetry.Instrumentation.AWS.Implementation.Tracing;

internal class AWSTracerProvider : TracerProvider
{
    public override Tracer GetTracer(string scope)
    {
        return new AWSTracer(scope);
    }
}
