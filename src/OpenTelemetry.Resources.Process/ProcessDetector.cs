// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.Process;

/// <summary>
///     Process detector.
/// </summary>
internal sealed class ProcessDetector : IResourceDetector
{
    /// <summary>
    ///     Detects the resource attributes for process.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        var attributes = new List<KeyValuePair<string, object>>(4)
        {
            new(ProcessSemanticConventions.AttributeProcessOwner, Environment.UserName),
#if NET
            new(ProcessSemanticConventions.AttributeProcessPid, Environment.ProcessId),
#else
            new(ProcessSemanticConventions.AttributeProcessPid, (long)GetProcessPid()),
#endif
        };

        // We only set the count to avoid the need to implement redaction.
        // See https://github.com/open-telemetry/semantic-conventions/blob/v1.40.0/docs/resource/process.md#selecting-process-attributes.
        var commandArgs = GetCommandLineArgs();

        if (commandArgs.Length > 0)
        {
            attributes.Add(new(ProcessSemanticConventions.AttributeProcessExecutablePath, commandArgs[0]));

            // Do not count the executable path as an argument
            int count = commandArgs.Length - 1;

            attributes.Add(new(ProcessSemanticConventions.AttributeProcessArgsCount, count));
        }

        return new Resource(attributes);

        static string[] GetCommandLineArgs()
        {
            try
            {
                return Environment.GetCommandLineArgs();
            }
            catch (NotSupportedException)
            {
                return [];
            }
        }

#if !NET
        static int GetProcessPid()
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            return process.Id;
        }
#endif
    }
}
