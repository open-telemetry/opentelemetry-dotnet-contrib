// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET6_0_OR_GREATER
using System;
#endif
using System.Collections.Generic;
using OpenTelemetry.Resources;

namespace OpenTelemetry.ResourceDetectors.Process;

/// <summary>
///     Process detector.
/// </summary>
public class ProcessDetector : IResourceDetector
{
    /// <summary>
    ///     Detects the resource attributes for process.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        return new Resource(new List<KeyValuePair<string, object>>(1)
        {
#if NET6_0_OR_GREATER
            new(ProcessSemanticConventions.AttributeProcessPid, Environment.ProcessId),
#else
            new(ProcessSemanticConventions.AttributeProcessPid, System.Diagnostics.Process.GetCurrentProcess().Id),
#endif
        });
    }
}
