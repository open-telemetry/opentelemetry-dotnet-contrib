// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.Process;

internal static class ProcessSemanticConventions
{
    public const string AttributeProcessOwner = "process.owner";
    public const string AttributeProcessPid = "process.pid";
    public const string AttributeProcessExecutablePath = "process.executable.path";
    public const string AttributeProcessArgsCount = "process.args_count";
}
