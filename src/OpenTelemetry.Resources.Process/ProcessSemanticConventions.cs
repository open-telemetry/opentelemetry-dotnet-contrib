// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.Process;

internal static class ProcessSemanticConventions
{
    public const string AttributeProcessArgsCount = "process.args_count";
    public const string AttributeProcessCommand = "process.command";
    public const string AttributeProcessCreationTime = "process.creation.time";
    public const string AttributeProcessExecutablePath = "process.executable.path";
    public const string AttributeProcessOwner = "process.owner";
    public const string AttributeProcessPid = "process.pid";
    public const string AttributeProcessTitle = "process.title";
    public const string AttributeProcessWorkingDirectory = "process.working_directory";
}
