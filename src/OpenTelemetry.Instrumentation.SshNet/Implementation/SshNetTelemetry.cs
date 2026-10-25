// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Instrumentation.SshNet.Implementation;

internal static class SshNetTelemetry
{
    internal const string ProtocolSftp = "sftp";
    internal const string ProtocolSsh = "ssh";

    internal const string AttributeFilePath = "file.path";
    internal const string AttributeOperationName = "ssh.operation.name";
    internal const string AttributeCommandText = "ssh.command.text";
    internal const string AttributeCommandExitCode = "ssh.command.exit_code";
    internal const string AttributeTargetPath = "sftp.target.path";
    internal const string AttributeTransferBytes = "sftp.transfer.bytes";

    /// <summary>
    /// The version of the OpenTelemetry semantic conventions the instrumentation targets. Shared by the
    /// activity source and the meter so the traces and metrics schemas cannot drift apart.
    /// </summary>
    internal static readonly Version SemanticConventionsVersion = new(1, 40, 0);

    internal static readonly ActivitySource ActivitySource = Trace.ActivitySourceFactory.Create(typeof(SshNetTelemetry), SemanticConventionsVersion);

    internal static readonly Meter Meter = Metrics.MeterFactory.Create(typeof(SshNetTelemetry), SemanticConventionsVersion);

    internal static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
        "ssh.client.operation.duration",
        unit: "s",
        description: "Duration of SSH and SFTP client operations.");
}
