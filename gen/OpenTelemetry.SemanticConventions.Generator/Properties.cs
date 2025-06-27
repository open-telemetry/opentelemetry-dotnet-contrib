// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace OpenTelemetry.SemanticConventions.Generator;

public readonly record struct Properties
{
    public readonly string AttributeName;
    public readonly string FileNamespace;
    public readonly string[] Values;

    public Properties(string fileNamespace, string attributeName, List<string> values)
    {
        AttributeName = attributeName;
        FileNamespace = fileNamespace;
        Values = values.ToArray();
    }
}
