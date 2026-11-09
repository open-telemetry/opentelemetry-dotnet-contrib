// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Hangfire.Tests;

internal sealed class TestJobWithDisposeException : IDisposable
{
    public const string Detail = "job-detail";

    public void Execute()
    {
    }

    public void Dispose()
    {
        throw new InvalidOperationException($"Failed to flush '{Detail}'.");
    }
}
