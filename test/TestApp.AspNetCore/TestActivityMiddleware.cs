// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace TestApp.AspNetCore;

#pragma warning disable CA1515
public class TestActivityMiddleware
#pragma warning restore CA1515
{
    public virtual void PreProcess(HttpContext context)
    {
        // Do nothing
    }

    public virtual void PostProcess(HttpContext context)
    {
        // Do nothing
    }
}
