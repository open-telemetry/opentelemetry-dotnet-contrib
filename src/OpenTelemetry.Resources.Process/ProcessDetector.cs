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
        using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        var attributes = new List<KeyValuePair<string, object>>(9)
        {
            new(ProcessSemanticConventions.AttributeProcessOwner, Environment.UserName),
            new(ProcessSemanticConventions.AttributeProcessArgsCount, Environment.GetCommandLineArgs().Length),
            new(ProcessSemanticConventions.AttributeProcessStartTime, currentProcess.StartTime.ToString("O") ?? string.Empty),
            new(ProcessSemanticConventions.AttributeProcessTitle, currentProcess.MainWindowTitle),
            new(ProcessSemanticConventions.AttributeProcessWorkingDir, Environment.CurrentDirectory),

            new(ProcessSemanticConventions.AttributeProcessExecName, currentProcess.ProcessName),
            new(ProcessSemanticConventions.AttributeProcessInteractive, Environment.UserInteractive),
#if NET
            new(ProcessSemanticConventions.AttributeProcessPid, Environment.ProcessId),
            new(ProcessSemanticConventions.AttributeProcessExecPath, Environment.ProcessPath ?? string.Empty),
        };
#else
            new(ProcessSemanticConventions.AttributeProcessPid, currentProcess.Id),
        };
#endif

        return new Resource(attributes);
    }
}
