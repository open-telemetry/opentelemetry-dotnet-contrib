// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.IO;

namespace OpenTelemetry.Exporter.Geneva.Tests;

internal class TestHelpers
{
    internal static string GenerateTempFilePath()
    {
        while (true)
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            if (!File.Exists(path))
            {
                return path;
            }
        }
    }
}
