// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;

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

        if (currentProcess.HasExited)
        {
            return Resource.Empty;
        }

        var attributes = new List<KeyValuePair<string, object>>(9)
        {
            new(ProcessSemanticConventions.AttributeProcessOwner, Environment.UserName),
            new(ProcessSemanticConventions.AttributeProcessArgsCount, Environment.GetCommandLineArgs().Length),
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

        try
        {
            attributes.Add(new(ProcessSemanticConventions.AttributeProcessStartTime, currentProcess.StartTime.ToString("O") ?? DateTime.Now.ToString("O")));
        }
        catch (Win32Exception)
        {
            attributes.Add(new(ProcessSemanticConventions.AttributeProcessStartTime, DateTime.Now.ToString("O")));
        }

        return new Resource(attributes);
    }
}
