// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace TestApp.AspNetCore;

#pragma warning disable CA1515
public class TestCallbackMiddleware
#pragma warning restore CA1515
{
    public virtual async Task<bool> ProcessAsync(HttpContext context)
    {
        return await Task.FromResult(true);
    }
}
