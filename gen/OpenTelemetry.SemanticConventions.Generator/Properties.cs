// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace OpenTelemetry.SemanticConventions.Generator;

internal readonly record struct Properties
{
    internal readonly string StructName;
    internal readonly string FileNamespace;
    internal readonly GenerationMode GenerationMode;
    internal readonly List<string> AttributeNames = new List<string>();

    internal Properties(string fileNamespace, string structName, GenerationMode generationMode)
    {
        this.StructName = structName;
        this.FileNamespace = fileNamespace;
        this.GenerationMode = generationMode;
    }
}
