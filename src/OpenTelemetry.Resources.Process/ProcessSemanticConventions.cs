// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.Process;

internal static class ProcessSemanticConventions
{
    public const string AttributeProcessOwner = "process.owner";
    public const string AttributeProcessPid = "process.pid";
    public const string AttributeProcessCommandLine = "process.command_line";
    public const string AttributeProcessCommandArgs = "process.command_args";
    public const string AttributeProcessExecPath = "process.executable.path";
    public const string AttributeProcessWorkingDir = "process.working.directory";
    public const string AttributeProcessArgsCount = "process.args.count";
    public const string AttributeProcessStartTime = "process.creation.time";
    public const string AttributeProcessExecName = "process.executable.name";
    public const string AttributeProcessInteractive = "process.interactive";
    public const string AttributeProcessTitle = "process.title";
}
