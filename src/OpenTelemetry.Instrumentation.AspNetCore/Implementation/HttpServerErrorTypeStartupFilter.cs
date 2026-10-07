// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation;

/// <summary>
/// Adds the <c>error.type</c> attribute to the built-in ASP.NET Core <c>http.server.request.duration</c>
/// metric when a request completes with a 5xx status code without an unhandled exception.
/// <para/>
/// ASP.NET Core 8.0-10.0 only set <c>error.type</c> when an exception is observed, whereas the
/// semantic conventions require it to be set to the status code for 5xx responses. ASP.NET Core
/// 11.0+ does this natively.
/// </summary>
/// <remarks>
/// The metric is recorded by the ASP.NET Core hosting layer after the request pipeline has
/// completed but before any diagnostic end events are raised, so the only way to influence its
/// tags is via <see cref="IHttpMetricsTagsFeature"/> from within the pipeline itself.
/// </remarks>
internal sealed class HttpServerErrorTypeStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        (app) =>
        {
            app.Use(static (next) => (context) => InvokeAsync(context, next));
            next(app);
        };

    internal static void AddErrorType(HttpContext context, IHttpMetricsTagsFeature feature)
    {
        var statusCode = context.Response.StatusCode;

        if (statusCode is < 500 or > 599)
        {
            return;
        }

        var tags = feature.Tags;

        // Middleware such as ExceptionHandlerMiddleware and DeveloperExceptionPageMiddleware add
        // the exception type as error.type if they handled an exception, which takes precedence.
        foreach (var tag in tags)
        {
            if (tag.Key == SemanticConventions.AttributeErrorType)
            {
                return;
            }
        }

        tags.Add(new(SemanticConventions.AttributeErrorType, TelemetryHelper.GetStatusCodeString(statusCode)));
    }

    private static Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // The feature is only present if the Microsoft.AspNetCore.Hosting meter is enabled
        if (context.Features.Get<IHttpMetricsTagsFeature>() is not { } feature)
        {
            return next(context);
        }

        var task = next(context);

        if (task.IsCompletedSuccessfully)
        {
            AddErrorType(context, feature);
            return Task.CompletedTask;
        }

        return AwaitRequestAsync(task, context, feature);

        static async Task AwaitRequestAsync(Task task, HttpContext context, IHttpMetricsTagsFeature feature)
        {
            // If an unhandled exception is thrown, ASP.NET Core adds the exception type as
            // error.type itself, so the status code is only used if the request succeeded.
            await task.ConfigureAwait(false);
            AddErrorType(context, feature);
        }
    }
}
#endif
