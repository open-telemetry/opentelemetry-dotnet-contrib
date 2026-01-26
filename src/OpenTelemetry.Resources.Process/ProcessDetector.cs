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

        bool isInteractive = Environment.UserInteractive &&
                             !Console.IsOutputRedirected &&
                             !Console.IsInputRedirected;

        var attributes = new List<KeyValuePair<string, object>>(9)
        {
            new(ProcessSemanticConventions.AttributeProcessOwner, Environment.UserName),
            new(ProcessSemanticConventions.AttributeProcessArgsCount, Environment.GetCommandLineArgs().Length),
            new(ProcessSemanticConventions.AttributeProcessTitle, currentProcess.ProcessName),
            new(ProcessSemanticConventions.AttributeProcessWorkingDir, Environment.CurrentDirectory),

            new(ProcessSemanticConventions.AttributeProcessInteractive, isInteractive),
            new(ProcessSemanticConventions.AttributeProcessPid, currentProcess.Id),
        };
        try
        {
            attributes.Add(new(ProcessSemanticConventions.AttributeProcessStartTime, currentProcess.StartTime.ToString("O")));
        }
        catch (Win32Exception)
        {
        }

        if (currentProcess.MainModule is not null)
        {
            attributes.Add(new(ProcessSemanticConventions.AttributeProcessExecName, currentProcess.MainModule.ModuleName));

            var fileInfo = new FileInfo(currentProcess.MainModule.FileName);
            attributes.Add(new(ProcessSemanticConventions.AttributeProcessExecPath, fileInfo.DirectoryName ?? string.Empty));
        }

        return new Resource(attributes);
    }
}
