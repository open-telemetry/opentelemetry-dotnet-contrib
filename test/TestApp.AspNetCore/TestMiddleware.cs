// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace TestApp.AspNetCore;

#pragma warning disable CA1515
public static class TestMiddleware
#pragma warning restore CA1515
{
    private static readonly AsyncLocal<Action<IApplicationBuilder>?> Current = new();

    public static IApplicationBuilder AddTestMiddleware(this IApplicationBuilder builder)
    {
        if (Current.Value is { } configure)
        {
            configure(builder);
        }

        return builder;
    }

    public static void Create(Action<IApplicationBuilder> action)
    {
        Current.Value = action;
    }
}
