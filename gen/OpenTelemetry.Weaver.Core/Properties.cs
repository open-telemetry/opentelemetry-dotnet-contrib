// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.IO;

namespace OpenTelemetry.Weaver;

internal readonly record struct Properties
{
    internal readonly string StructName;
    internal readonly string FileNamespace;
    internal readonly Dictionary<GenerationMode, Stream> Streams = new Dictionary<GenerationMode, Stream>();

    internal Properties(string fileNamespace, string structName)
    {
        this.StructName = structName;
        this.FileNamespace = fileNamespace;
    }
}
