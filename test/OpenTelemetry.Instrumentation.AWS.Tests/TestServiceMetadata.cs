// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Runtime.Internal;

namespace OpenTelemetry.Instrumentation.AWS.Tests;

internal sealed class TestServiceMetadata : IServiceMetadata
{
    public string ServiceId => "Test";

    public IDictionary<string, string> OperationNameMapping { get; } = new Dictionary<string, string>();
}
