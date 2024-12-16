// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Geneva;

/// <summary>
/// Contains the exception stack trace export mode defintions.
/// </summary>
public enum ExceptionStackExportMode
{
    /// <summary>
    /// Exception stack traces are dropped.
    /// </summary>
    Drop,

    /// <summary>
    /// Exception stack traces are exported as string, using a culture agnostic representation of the results of ToString() implementation of the Exception.
    /// Though formatted in culture-agnostic way, this is more targetted towards human readability.
    /// See <see href="https://learn.microsoft.com/dotnet/api/system.exception.tostring"/>.
    /// </summary>
    ExportAsString,

    /// <summary>
    /// Exception stack traces are exported as string, using the StackTrace property of the Exception.
    /// See <see href="https://learn.microsoft.com/dotnet/api/system.exception.tostring"/>.
    /// </summary>
    ExportAsStackTraceString,

    // ExportAsArrayOfStacks - future if stacks can be exported in more structured way
}
