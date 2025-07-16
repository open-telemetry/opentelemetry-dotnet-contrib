// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace OpenTelemetry.SemanticConventions.Generator;

internal readonly record struct Properties
{
    internal readonly string StructName;
    internal readonly string FileNamespace;
    internal readonly Dictionary<GenerationMode, List<string>> Values = new Dictionary<GenerationMode, List<string>>();

    internal Properties(string fileNamespace, string structName)
    {
        this.StructName = structName;
        this.FileNamespace = fileNamespace;
    }
}
