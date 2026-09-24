// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// This class is a temporary shim until https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5379 is merged

namespace Xunit;

public sealed class TestContext
{
    public static TestContext Current { get; } = new();

    public CancellationToken CancellationToken { get; } = CancellationToken.None;
}
