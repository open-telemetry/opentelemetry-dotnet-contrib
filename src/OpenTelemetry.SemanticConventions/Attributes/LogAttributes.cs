// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from 'src\OpenTelemetry.SemanticConventions\scripts\templates\registry\SemanticConventionsAttributes.cs.j2' </auto-generated>

#nullable enable

#pragma warning disable CS1570 // XML comment has badly formed XML

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class LogAttributes
{
    /// <summary>
    /// The basename of the file
    /// </summary>
    public const string AttributeLogFileName = "log.file.name";

    /// <summary>
    /// The basename of the file, with symlinks resolved
    /// </summary>
    public const string AttributeLogFileNameResolved = "log.file.name_resolved";

    /// <summary>
    /// The full path to the file
    /// </summary>
    public const string AttributeLogFilePath = "log.file.path";

    /// <summary>
    /// The full path to the file, with symlinks resolved
    /// </summary>
    public const string AttributeLogFilePathResolved = "log.file.path_resolved";

    /// <summary>
    /// The stream associated with the log. See below for a list of well-known values
    /// </summary>
    public const string AttributeLogIostream = "log.iostream";

    /// <summary>
    /// A unique identifier for the Log Record
    /// </summary>
    /// <remarks>
    /// If an id is provided, other log records with the same id will be considered duplicates and can be removed safely. This means, that two distinguishable log records MUST have different values.
    /// The id MAY be an <a href="https://github.com/ulid/spec">Universally Unique Lexicographically Sortable Identifier (ULID)</a>, but other identifiers (e.g. UUID) may be used as needed
    /// </remarks>
    public const string AttributeLogRecordUid = "log.record.uid";

    /// <summary>
    /// The stream associated with the log. See below for a list of well-known values
    /// </summary>
    public static class LogIostreamValues
    {
        /// <summary>
        /// Logs from stdout stream
        /// </summary>
        public const string Stdout = "stdout";

        /// <summary>
        /// Events from stderr stream
        /// </summary>
        public const string Stderr = "stderr";
    }
}
