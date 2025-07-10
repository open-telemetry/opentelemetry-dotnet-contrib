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
        var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
#pragma warning disable CA1305 // Specify IFormatProvider
        return new Resource(new List<KeyValuePair<string, object>>(2)
        {
            new(ProcessSemanticConventions.AttributeProcessOwner, Environment.UserName),
            new(ProcessSemanticConventions.AttributeProcessCommandLine, Environment.CommandLine),
            new(ProcessSemanticConventions.AttributeProcessCommandArgs, Environment.GetCommandLineArgs()),
            new(ProcessSemanticConventions.AttributeProcessArgsCount, Environment.GetCommandLineArgs().Length),
            new(ProcessSemanticConventions.AttributeProcessStartTime, currentProcess.StartTime.ToString() ?? string.Empty),
            new(ProcessSemanticConventions.AttributeProcessTitle, currentProcess.MainWindowTitle),
            new(ProcessSemanticConventions.AttributeProcessWorkingDir, Environment.CurrentDirectory),

            new(ProcessSemanticConventions.AttributeProcessExecName, currentProcess.ProcessName),
            new(ProcessSemanticConventions.AttributeProcessInteractive, Environment.UserInteractive),
#if NET
            new(ProcessSemanticConventions.AttributeProcessPid, Environment.ProcessId),
            new(ProcessSemanticConventions.AttributeProcessExecPath, Environment.ProcessPath ?? string.Empty),
        });
#pragma warning restore CA1305 // Specify IFormatProvider
#else
            new(ProcessSemanticConventions.AttributeProcessPid, GetProcessPid()),
        });
        static int GetProcessPid()
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            return process.Id;
        }
#endif
    }
}
