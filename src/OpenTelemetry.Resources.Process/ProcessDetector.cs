// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using CurrentProcess = System.Diagnostics.Process;

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
        GetProcessAttributes(
            out int processId,
            out DateTime? creationTime,
            out string? processPath,
            out string? title);

        var attributes = new List<KeyValuePair<string, object>>(8)
        {
            new(ProcessSemanticConventions.AttributeProcessOwner, Environment.UserName),
            new(ProcessSemanticConventions.AttributeProcessPid, processId),
            new(ProcessSemanticConventions.AttributeProcessWorkingDirectory, Environment.CurrentDirectory),
        };

        if (creationTime is { } startTime)
        {
            attributes.Add(new(ProcessSemanticConventions.AttributeProcessCreationTime, startTime.ToString("O", CultureInfo.InvariantCulture)));
        }

        if (title is { Length: > 0 })
        {
            attributes.Add(new(ProcessSemanticConventions.AttributeProcessTitle, title));
        }

#if NET
        processPath ??= Environment.ProcessPath;
#endif

        // We only set the count to avoid the need to implement redaction.
        // See https://github.com/open-telemetry/semantic-conventions/blob/v1.40.0/docs/resource/process.md#selecting-process-attributes.
        var commandArgs = GetCommandLineArgs();

        if (commandArgs is not null)
        {
            attributes.Add(new(ProcessSemanticConventions.AttributeProcessArgsCount, commandArgs.Length));
        }

        if (!string.IsNullOrEmpty(processPath))
        {
            attributes.Add(new(ProcessSemanticConventions.AttributeProcessExecutablePath, processPath!));
        }

        return new Resource(attributes);

        static string[]? GetCommandLineArgs()
        {
            try
            {
                return Environment.GetCommandLineArgs();
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

        static void GetProcessAttributes(
            out int processId,
            out DateTime? creationTime,
            out string? processPath,
            out string? title)
        {
            using var process = CurrentProcess.GetCurrentProcess();
            processId = process.Id;

            creationTime = SafeGet(process, (p) => p.StartTime);
            processPath = SafeGet(process, (p) => p.MainModule?.FileName);
            title = SafeGet(process, (p) => p.MainWindowTitle);

            static T? SafeGet<T>(CurrentProcess process, Func<CurrentProcess, T> getter)
            {
                try
                {
                    return getter(process);
                }
                catch (Exception)
                {
                    return default;
                }
            }
        }
    }
}
