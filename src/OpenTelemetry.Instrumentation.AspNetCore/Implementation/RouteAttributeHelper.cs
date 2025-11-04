// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETSTANDARD

using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation;

internal static class RouteAttributeHelper
{
    public static void AddRouteAttribute(this TagList tags, RouteEndpoint endpoint, HttpRequest request)
    {
        var routePattern = GetRoutePattern(endpoint.RoutePattern, request.RouteValues);

        if (!string.IsNullOrEmpty(routePattern))
        {
            tags.Add(new KeyValuePair<string, object?>(SemanticConventions.AttributeHttpRoute, routePattern));
        }
    }

    public static void SetRouteAttributeTag(this Activity activity, RouteEndpoint endpoint, HttpRequest request)
    {
        var routePattern = GetRoutePattern(endpoint.RoutePattern, request.RouteValues);

        if (!string.IsNullOrEmpty(routePattern))
        {
            TelemetryHelper.RequestDataHelper.SetActivityDisplayName(activity, request.Method, routePattern);
            activity.SetTag(SemanticConventions.AttributeHttpRoute, routePattern);
        }
    }

    private static string GetRoutePattern(RoutePattern routePattern, RouteValueDictionary routeValues)
    {
        if (routePattern.PathSegments.Count == 0)
        {
            // RazorPage default route
            if (routePattern.Defaults.TryGetValue("page", out var pageValue))
            {
                return pageValue?.ToString()?.Trim('/')
                    ?? string.Empty;
            }

            return string.Empty;
        }

        var sb = new StringBuilder();

        foreach (var segment in routePattern.PathSegments)
        {
            foreach (var part in segment.Parts)
            {
                if (part is RoutePatternLiteralPart literalPart)
                {
                    sb.Append(literalPart.Content);
                    sb.Append('/');
                }
                else if (part is RoutePatternParameterPart parameterPart)
                {
                    switch (parameterPart.Name)
                    {
                        case "area":
                        case "controller":
                        case "action":
                            routePattern.RequiredValues.TryGetValue(parameterPart.Name, out var parameterValue);
                            if (parameterValue != null)
                            {
                                sb.Append(parameterValue);
                                sb.Append('/');
                                break;
                            }

                            goto default;
                        default:
                            if (!parameterPart.IsOptional ||
                                (parameterPart.IsOptional && routeValues.ContainsKey(parameterPart.Name)))
                            {
                                sb.Append('{');
                                sb.Append(parameterPart.Name);
                                sb.Append('}');
                                sb.Append('/');
                            }

                            break;
                    }
                }
            }
        }

        // Remove the trailing '/'
        return sb.ToString(0, sb.Length - 1);
    }
}

#endif
