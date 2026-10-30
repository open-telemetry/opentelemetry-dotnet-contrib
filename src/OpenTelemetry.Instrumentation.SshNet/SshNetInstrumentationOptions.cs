// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.SshNet;

/// <summary>
/// Options for SSH.NET instrumentation.
/// </summary>
public class SshNetInstrumentationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether remote file paths are added
    /// as the <c>file.path</c> (and <c>sftp.target.path</c>) span attributes.
    /// Default value: <see langword="true"/>.
    /// </summary>
    public bool RecordFilePath { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the text of executed SSH
    /// commands is added as the <c>ssh.command.text</c> span attribute.
    /// Command text may contain sensitive data, so this is opt-in.
    /// Default value: <see langword="false"/>.
    /// </summary>
    public bool RecordCommandText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an exception event is added
    /// to the span when an operation fails.
    /// Default value: <see langword="false"/>.
    /// </summary>
    public bool RecordException { get; set; }
}
