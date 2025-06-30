// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace OpenTelemetry.SemanticConventions.Generator;

internal readonly record struct Properties
{
    internal readonly string AttributeName;
    internal readonly string FileNamespace;
    internal readonly List<string> Values;

    internal Properties(string fileNamespace, string attributeName, List<string> values)
    {
        this.AttributeName = attributeName;
        this.FileNamespace = fileNamespace;
        this.Values = values;
    }
}
