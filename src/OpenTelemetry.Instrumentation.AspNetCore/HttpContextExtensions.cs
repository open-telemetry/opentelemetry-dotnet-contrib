// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace OpenTelemetry.Instrumentation.AspNetCore;

internal static class HttpContextExtensions
{
    public static string? GetHttpRoute(this HttpContext context)
    {
        var endpoint = context.Features.Get<IExceptionHandlerPathFeature>()?.Endpoint as RouteEndpoint ??
                       context.GetEndpoint() as RouteEndpoint;

        return endpoint?.RoutePattern.RawText;
    }
}
#endif
