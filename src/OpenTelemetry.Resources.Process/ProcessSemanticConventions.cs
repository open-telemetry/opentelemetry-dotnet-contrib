// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.Process;

internal static class ProcessSemanticConventions
{
    public const string AttributeProcessOwner = "process.owner";
    public const string AttributeProcessPid = "process.pid";
    public const string AttributeProcessExecPath = "process.executable.path";
    public const string AttributeProcessWorkingDir = "process.working_directory";
    public const string AttributeProcessArgsCount = "process.args_count";
    public const string AttributeProcessStartTime = "process.creation.time";
    public const string AttributeProcessExecName = "process.executable.name";
    public const string AttributeProcessInteractive = "process.interactive";
    public const string AttributeProcessTitle = "process.title";
}
