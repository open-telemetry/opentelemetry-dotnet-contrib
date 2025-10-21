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
        var sb = new StringBuilder();

        for (var i = 0; i < routePattern.PathSegments.Count; i++)
        {
            var segment = routePattern.PathSegments[i];

            foreach (var part in segment.Parts)
            {
                if (part is RoutePatternLiteralPart literalPart)
                {
                    sb.Append(literalPart.Content);
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
                                break;
                            }

                            goto default;
                        default:
                            if (routeValues.ContainsKey(parameterPart.Name))
                            {
                                sb.Append('{');
                                sb.Append(parameterPart.Name);
                                sb.Append('}');
                            }

                            break;
                    }
                }
            }

            if (i < routePattern.PathSegments.Count - 1)
            {
                sb.Append('/');
            }
        }

        return sb.ToString();
    }
}

#endif
