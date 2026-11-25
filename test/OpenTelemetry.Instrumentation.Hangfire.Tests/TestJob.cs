// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Instrumentation.Hangfire.Tests;

internal class TestJob
{
    public static readonly ActivitySource ActivitySource = new(nameof(TestJob));

    public void Execute()
    {
    }

    public void ExecuteWithChildActivity()
    {
        using var activity = ActivitySource.StartActivity(nameof(this.ExecuteWithChildActivity));
    }

    public void ThrowException()
    {
        throw new Exception();
    }
}
