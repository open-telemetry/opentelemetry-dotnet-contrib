// <copyright file="FileStorageTests.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Text;
using System.Threading;
using Xunit;

namespace OpenTelemetry.Contrib.PersistentStorage.Tests
{
    public class FileStorageTests
    {
        [Fact]
        public void FileStorage_E2E_Test()
        {
            var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            using var storage = new FileStorage(testDirectory.FullName);

            var data = Encoding.UTF8.GetBytes("Hello, World!");

            // Create blob.
            IPersistentBlob blob1 = storage.CreateBlob(data);

            // Get blob.
            IPersistentBlob blob2 = storage.GetBlob();

            Assert.Single(storage.GetBlobs());

            // Verify file name from both create blob and get blob are same.
            Assert.Equal(((FileBlob)blob1).FullPath, ((FileBlob)blob2).FullPath);

            // Validate if content in the blob is same as buffer data passed to create blob.
            Assert.Equal(data, blob1.Read());

            testDirectory.Delete(true);
        }

        [Fact]
        public void FileStorageTests_Lease()
        {
            var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            using var storage = new FileStorage(testDirectory.FullName, 10000, 500);

            var data = Encoding.UTF8.GetBytes("Hello, World!");

            // Create a blob and lease for 100 ms.
            IPersistentBlob blob = storage.CreateBlob(data, 100);

            // Leasing a blob will change the extension of a file to .lock
            Assert.EndsWith(".lock", ((FileBlob)blob).FullPath);

            // Maintenance timer will validate will run every 500ms.
            // Sleep for a minute so that mainenance timer will remove the lease on the blob.
            Thread.Sleep(TimeSpan.FromSeconds(1));

            // Lease is released, file name has changed.
            Assert.False(File.Exists(((FileBlob)blob).FullPath));

            testDirectory.Delete(true);
        }
    }
}
