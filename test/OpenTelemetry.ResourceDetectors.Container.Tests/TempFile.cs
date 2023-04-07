// <copyright file="TempFile.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.IO;
using System.Threading;

namespace OpenTelemetry.ResourceDetectors.Container.Tests;

internal class TempFile : IDisposable
{
    public TempFile()
    {
        this.FilePath = Path.GetTempFileName();
    }

    public string FilePath { get; set; }

    public void Write(string data)
    {
        using (var stream = new FileStream(this.FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            using (var sw = new StreamWriter(stream))
            {
                sw.Write(data);
            }
        }
    }

    public void Dispose()
    {
        for (var tries = 0; ; tries++)
        {
            try
            {
                File.Delete(this.FilePath);
                return;
            }
            catch (IOException) when (tries < 3)
            {
                // the file is unavailable because it is: still being written to or being processed by another thread
                // sleep for sometime before deleting
                Thread.Sleep(1000);
            }
        }
    }
}
