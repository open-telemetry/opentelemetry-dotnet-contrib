// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from 'src\OpenTelemetry.SemanticConventions\scripts\templates\registry\SemanticConventionsAttributes.cs.j2' </auto-generated>

#nullable enable

#pragma warning disable CS1570 // XML comment has badly formed XML

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class ProcessAttributes
{
    /// <summary>
    /// The command used to launch the process (i.e. the command name). On Linux based systems, can be set to the zeroth string in <c>proc/[pid]/cmdline</c>. On Windows, can be set to the first parameter extracted from <c>GetCommandLineW</c>
    /// </summary>
    public const string AttributeProcessCommand = "process.command";

    /// <summary>
    /// All the command arguments (including the command/executable itself) as received by the process. On Linux-based systems (and some other Unixoid systems supporting procfs), can be set according to the list of null-delimited strings extracted from <c>proc/[pid]/cmdline</c>. For libc-based executables, this would be the full argv vector passed to <c>main</c>
    /// </summary>
    public const string AttributeProcessCommandArgs = "process.command_args";

    /// <summary>
    /// The full command used to launch the process as a single string representing the full command. On Windows, can be set to the result of <c>GetCommandLineW</c>. Do not set this if you have to assemble it just for monitoring; use <c>process.command_args</c> instead
    /// </summary>
    public const string AttributeProcessCommandLine = "process.command_line";

    /// <summary>
    /// Specifies whether the context switches for this data point were voluntary or involuntary
    /// </summary>
    public const string AttributeProcessContextSwitchType = "process.context_switch_type";

    /// <summary>
    /// The CPU state of the process
    /// </summary>
    public const string AttributeProcessCpuState = "process.cpu.state";

    /// <summary>
    /// The date and time the process was created, in ISO 8601 format
    /// </summary>
    public const string AttributeProcessCreationTime = "process.creation.time";

    /// <summary>
    /// The name of the process executable. On Linux based systems, can be set to the <c>Name</c> in <c>proc/[pid]/status</c>. On Windows, can be set to the base name of <c>GetProcessImageFileNameW</c>
    /// </summary>
    public const string AttributeProcessExecutableName = "process.executable.name";

    /// <summary>
    /// The full path to the process executable. On Linux based systems, can be set to the target of <c>proc/[pid]/exe</c>. On Windows, can be set to the result of <c>GetProcessImageFileNameW</c>
    /// </summary>
    public const string AttributeProcessExecutablePath = "process.executable.path";

    /// <summary>
    /// The exit code of the process
    /// </summary>
    public const string AttributeProcessExitCode = "process.exit.code";

    /// <summary>
    /// The date and time the process exited, in ISO 8601 format
    /// </summary>
    public const string AttributeProcessExitTime = "process.exit.time";

    /// <summary>
    /// The PID of the process's group leader. This is also the process group ID (PGID) of the process
    /// </summary>
    public const string AttributeProcessGroupLeaderPid = "process.group_leader.pid";

    /// <summary>
    /// Whether the process is connected to an interactive shell
    /// </summary>
    public const string AttributeProcessInteractive = "process.interactive";

    /// <summary>
    /// The username of the user that owns the process
    /// </summary>
    public const string AttributeProcessOwner = "process.owner";

    /// <summary>
    /// The type of page fault for this data point. Type <c>major</c> is for major/hard page faults, and <c>minor</c> is for minor/soft page faults
    /// </summary>
    public const string AttributeProcessPagingFaultType = "process.paging.fault_type";

    /// <summary>
    /// Parent Process identifier (PPID)
    /// </summary>
    public const string AttributeProcessParentPid = "process.parent_pid";

    /// <summary>
    /// Process identifier (PID)
    /// </summary>
    public const string AttributeProcessPid = "process.pid";

    /// <summary>
    /// The real user ID (RUID) of the process
    /// </summary>
    public const string AttributeProcessRealUserId = "process.real_user.id";

    /// <summary>
    /// The username of the real user of the process
    /// </summary>
    public const string AttributeProcessRealUserName = "process.real_user.name";

    /// <summary>
    /// An additional description about the runtime of the process, for example a specific vendor customization of the runtime environment
    /// </summary>
    public const string AttributeProcessRuntimeDescription = "process.runtime.description";

    /// <summary>
    /// The name of the runtime of this process. For compiled native binaries, this SHOULD be the name of the compiler
    /// </summary>
    public const string AttributeProcessRuntimeName = "process.runtime.name";

    /// <summary>
    /// The version of the runtime of this process, as returned by the runtime without modification
    /// </summary>
    public const string AttributeProcessRuntimeVersion = "process.runtime.version";

    /// <summary>
    /// The saved user ID (SUID) of the process
    /// </summary>
    public const string AttributeProcessSavedUserId = "process.saved_user.id";

    /// <summary>
    /// The username of the saved user
    /// </summary>
    public const string AttributeProcessSavedUserName = "process.saved_user.name";

    /// <summary>
    /// The PID of the process's session leader. This is also the session ID (SID) of the process
    /// </summary>
    public const string AttributeProcessSessionLeaderPid = "process.session_leader.pid";

    /// <summary>
    /// The effective user ID (EUID) of the process
    /// </summary>
    public const string AttributeProcessUserId = "process.user.id";

    /// <summary>
    /// The username of the effective user of the process
    /// </summary>
    public const string AttributeProcessUserName = "process.user.name";

    /// <summary>
    /// Virtual process identifier
    /// </summary>
    /// <remarks>
    /// The process ID within a PID namespace. This is not necessarily unique across all processes on the host but it is unique within the process namespace that the process exists within
    /// </remarks>
    public const string AttributeProcessVpid = "process.vpid";

    /// <summary>
    /// Specifies whether the context switches for this data point were voluntary or involuntary
    /// </summary>
    public static class ProcessContextSwitchTypeValues
    {
        /// <summary>
        /// voluntary
        /// </summary>
        public const string Voluntary = "voluntary";

        /// <summary>
        /// involuntary
        /// </summary>
        public const string Involuntary = "involuntary";
    }

    /// <summary>
    /// The CPU state of the process
    /// </summary>
    public static class ProcessCpuStateValues
    {
        /// <summary>
        /// system
        /// </summary>
        public const string System = "system";

        /// <summary>
        /// user
        /// </summary>
        public const string User = "user";

        /// <summary>
        /// wait
        /// </summary>
        public const string Wait = "wait";
    }

    /// <summary>
    /// The type of page fault for this data point. Type <c>major</c> is for major/hard page faults, and <c>minor</c> is for minor/soft page faults
    /// </summary>
    public static class ProcessPagingFaultTypeValues
    {
        /// <summary>
        /// major
        /// </summary>
        public const string Major = "major";

        /// <summary>
        /// minor
        /// </summary>
        public const string Minor = "minor";
    }
}
