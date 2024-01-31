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
        catch (InvalidOperationException ex)
        {
            // Handling InvalidOperationException due to https://learn.microsoft.com/en-us/dotnet/api/system.environment.machinename#exceptions
            HostResourceEventSource.Log.ResourceAttributesExtractException(nameof(HostDetector), ex);
        }

        return Resource.Empty;
    }
}
