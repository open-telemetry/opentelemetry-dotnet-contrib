// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using Xunit;

namespace OpenTelemetry.Tests;

/// <summary>
/// This <see cref="TheoryAttribute" /> skips tests if the required Docker engine is not available.
/// </summary>
internal class EnabledOnDockerPlatformTheoryAttribute : TheoryAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnabledOnDockerPlatformTheoryAttribute" /> class.
    /// </summary>
    public EnabledOnDockerPlatformTheoryAttribute(DockerPlatform dockerPlatform)
    {
        const string executable = "docker";

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        void AppendStdout(object sender, DataReceivedEventArgs e)
        {
            stdout.Append(e.Data);
        }

        void AppendStderr(object sender, DataReceivedEventArgs e)
        {
            stderr.Append(e.Data);
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = string.Join(" ", "version", "--format '{{.Server.Os}}'"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        var process = new Process
        {
            StartInfo = processStartInfo,
        };
        process.OutputDataReceived += AppendStdout;
        process.ErrorDataReceived += AppendStderr;

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
        }
        finally
        {
            process.OutputDataReceived -= AppendStdout;
            process.ErrorDataReceived -= AppendStderr;
        }

        if (0.Equals(process.ExitCode) && stdout.ToString().IndexOf(dockerPlatform.ToString(), StringComparison.OrdinalIgnoreCase) > 0)
        {
            return;
        }

        this.Skip = $"The Docker {dockerPlatform} engine is not available.";
    }

    internal enum DockerPlatform
    {
        /// <summary>
        /// Docker Linux engine.
        /// </summary>
        Linux,

        /// <summary>
        /// Docker Windows engine.
        /// </summary>
        Windows,
    }
}
