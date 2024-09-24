// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.Process;

/// <summary>
///     Process detector.
/// </summary>
internal sealed class ProcessDetector : IResourceDetector
{
    private readonly bool includeProcessOwner;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessDetector"/> class.
    /// </summary>
    /// <param name="includeProcessOwner">Determines whether to include process owner in the resource attributes.</param>
    public ProcessDetector(bool includeProcessOwner = true)
    {
        this.includeProcessOwner = includeProcessOwner;
    }

    /// <summary>
    ///     Detects the resource attributes for process.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        var attributes = new List<KeyValuePair<string, object>>(2);

        if (this.includeProcessOwner)
        {
            attributes.Add(new(ProcessSemanticConventions.AttributeProcessOwner, Environment.UserName));
        }

#if NET
        attributes.Add(new(ProcessSemanticConventions.AttributeProcessPid, Environment.ProcessId));
#else
        attributes.Add(new(ProcessSemanticConventions.AttributeProcessPid, System.Diagnostics.Process.GetCurrentProcess().Id));
#endif

        return new Resource(attributes);
    }
}
