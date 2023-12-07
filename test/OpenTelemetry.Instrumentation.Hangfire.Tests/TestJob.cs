// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;

namespace OpenTelemetry.Instrumentation.Hangfire.Tests;

public class TestJob
{
    public void Execute()
    {
    }

    public void ThrowException()
    {
        throw new Exception();
    }
}
