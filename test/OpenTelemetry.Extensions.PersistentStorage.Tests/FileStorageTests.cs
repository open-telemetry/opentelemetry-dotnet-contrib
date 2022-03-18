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

namespace OpenTelemetry.Extensions.PersistentStorage.Tests
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
        public void FileStorage_CreateBlobReturnsNullIfStorageIsFull()
        {
            var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            using var storage = new FileStorage(testDirectory.FullName, 10000);

            PersistentStorageHelper.UpdateDirectorySize(10000);

            var data = Encoding.UTF8.GetBytes("Hello, World!");

            Assert.Null(storage.CreateBlob(data));

            testDirectory.Delete(true);
        }

        [Fact]
        public void FileStorage_PathIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => new FileStorage(null));
        }

        [Fact]
        public void FileStorage_TestRetentionPeriod()
        {
            var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            long maxSizeInBytes = 100000;
            int maintenancePeriodInMilliseconds = 3000;
            int retentionPeriodInMilliseconds = 2000;
            int writeTimeOutInMilliseconds = 1000;
            using var storage = new FileStorage(
                testDirectory.FullName,
                maxSizeInBytes,
                maintenancePeriodInMilliseconds,
                retentionPeriodInMilliseconds,
                writeTimeOutInMilliseconds);

            var data = Encoding.UTF8.GetBytes("Hello, World!");
            var blob1 = (FileBlob)storage.CreateBlob(data);

            // Wait for maintenance job to run
            Thread.Sleep(4000);

            // Blob will be deleted as retention period is 1 sec
            Assert.False(File.Exists(blob1.FullPath));

            testDirectory.Delete(true);
        }

        [Fact]
        public void FileStorage_TestWriteTimeoutPeriod()
        {
            var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            long maxSizeInBytes = 100000;
            int maintenancePeriodInMilliseconds = 3000;
            int retentionPeriodInMilliseconds = 2000;
            int writeTimeOutInMilliseconds = 1000;
            using var storage = new FileStorage(
                testDirectory.FullName,
                maxSizeInBytes,
                maintenancePeriodInMilliseconds,
                retentionPeriodInMilliseconds,
                writeTimeOutInMilliseconds);

            var data = Encoding.UTF8.GetBytes("Hello, World!");
            var blob2 = (FileBlob)storage.CreateBlob(data);

            // Mock write
            File.Move(blob2.FullPath, blob2.FullPath + ".tmp");

            // validate file moved successfully
            Assert.True(File.Exists(blob2.FullPath + ".tmp"));

            // Wait for maintenance job to run
            Thread.Sleep(4000);

            // tmp file will be deleted as write timeout period is 1 sec
            Assert.False(File.Exists(blob2.FullPath + ".tmp"));
            Assert.False(File.Exists(blob2.FullPath));

            testDirectory.Delete(true);
        }

        [Fact]
        public void FileStorageTests_TestLeaseExpiration()
        {
            var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            long maxSizeInBytes = 100000;
            int maintenancePeriodInMilliseconds = 3000;
            int retentionPeriodInMilliseconds = 2000;
            int writeTimeOutInMilliseconds = 1000;
            using var storage = new FileStorage(
                testDirectory.FullName,
                maxSizeInBytes,
                maintenancePeriodInMilliseconds,
                retentionPeriodInMilliseconds,
                writeTimeOutInMilliseconds);

            var data = Encoding.UTF8.GetBytes("Hello, World!");
            var blob = (FileBlob)storage.CreateBlob(data);
            var blobPath = blob.FullPath;

            blob.Lease(1000);
            var leasePath = blob.FullPath;
            Assert.True(File.Exists(leasePath));

            // Wait for maintenance job to run
            Thread.Sleep(4000);

            // File name will be change to .blob
            Assert.True(File.Exists(blobPath));
            Assert.False(File.Exists(leasePath));

            testDirectory.Delete(true);
        }
    }
}
