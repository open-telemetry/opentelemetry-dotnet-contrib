// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.DynamicControl.Tests;

internal static class WaitHelper
{
    public static async Task WaitUntil(Func<bool> condition, int timeoutMilliseconds = 10_000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMilliseconds)
            {
                Assert.Fail("Timed out waiting for condition.");
            }

            await Task.Delay(5);
        }
    }
}
