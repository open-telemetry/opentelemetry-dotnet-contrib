// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

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
    /// Exception stack traces are exported as strings.
    /// </summary>
    ExportAsString,

    // ExportAsArrayOfStacks - future if stacks can be exported in more structured way
}
