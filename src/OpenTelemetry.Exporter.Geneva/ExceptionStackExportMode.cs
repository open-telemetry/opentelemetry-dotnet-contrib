// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Geneva;

public enum ExceptionStackExportMode
{
    Drop,
    ExportAsString,

    // ExportAsArrayOfStacks - future if stacks can be exported in more structured way
}
