// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using OpenTelemetry.Resources;

namespace OpenTelemetry.ResourceDetectors.Host;

/// <summary>
/// Host detector.
/// </summary>
public sealed class HostDetector : IResourceDetector
{
    /// <summary>
    /// Detects the resource attributes from host.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        try
        {
            return new Resource(new List<KeyValuePair<string, object>>(1)
            {
                new(HostSemanticConventions.AttributeHostName, Environment.MachineName),
            });
        }
        catch (InvalidOperationException)
        {
            // TODO replace comment by logging mechanism. Tracked under https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/1514
        }

        return Resource.Empty;
    }
}
