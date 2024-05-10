// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using OpenTelemetry.Resources;

namespace OpenTelemetry.ResourceDetectors.Process;

/// <summary>
///     Process detector.
/// </summary>
public sealed class ProcessDetector : IResourceDetector
{
    /// <summary>
    ///     Detects the resource attributes for process.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        return new Resource(new List<KeyValuePair<string, object>>(2)
        {
            new(ProcessSemanticConventions.AttributeProcessOwner, Environment.UserName),
#if NET6_0_OR_GREATER
            new(ProcessSemanticConventions.AttributeProcessPid, Environment.ProcessId),
#else
            new(ProcessSemanticConventions.AttributeProcessPid, System.Diagnostics.Process.GetCurrentProcess().Id),
#endif
        });
    }
}
