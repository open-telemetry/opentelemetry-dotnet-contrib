// <copyright file="FileBlobTests.cs" company="OpenTelemetry Authors">
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
using System.Text;
using System.Threading;
using Xunit;

namespace OpenTelemetry.Extensions.PersistentStorage.Tests
{
    public class FileBlobTests
    {
        [Fact]
        public void FileBlobTests_E2E_Test()
        {
            var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            IPersistentBlob blob = new FileBlob(testFile.FullName);

            var data = Encoding.UTF8.GetBytes("Hello, World!");
            IPersistentBlob blob1 = blob.Write(data);
            var blobContent = blob.Read();

            Assert.Equal(testFile.FullName, ((FileBlob)blob1).FullPath);
            Assert.Equal(data, blobContent);

            blob1.Delete();
            Assert.False(testFile.Exists);
        }

        [Fact]
        public void FileBlobTests_Lease()
        {
            var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            IPersistentBlob blob = new FileBlob(testFile.FullName);

            var data = Encoding.UTF8.GetBytes("Hello, World!");
            var leasePeriodMilliseconds = 1000;
            IPersistentBlob blob1 = blob.Write(data);
            IPersistentBlob leasedBlob = blob1.Lease(leasePeriodMilliseconds);

            Assert.Contains(".lock", ((FileBlob)leasedBlob).FullPath);

            blob1.Delete();
            Assert.False(testFile.Exists);
        }

        [Fact]
        public void FileBlobTests_LeaseAfterDelete()
        {
            var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            IPersistentBlob blob = new FileBlob(testFile.FullName);

            var data = Encoding.UTF8.GetBytes("Hello, World!");
            blob.Write(data);
            blob.Delete();

            // Lease should return null
            Assert.Null(blob.Lease(1000));
        }

        [Fact]
        public void FileBlobTests_ReadFailsOnAlreadyLeasedFile()
        {
            var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            FileBlob blob1 = new FileBlob(testFile.FullName);
            FileBlob blob2 = new FileBlob(testFile.FullName);
            var data = Encoding.UTF8.GetBytes("Hello, World!");
            blob1.Write(data);
            var leasePeriodMilliseconds = 10000;

            // Leased by another thread/process/object
            blob2.Lease(leasePeriodMilliseconds);

            // Read should fail as file is leased
            Assert.Null(blob1.Read());

            // Clean up
            blob2.Delete();
        }

        [Fact]
        public void FileBlobTests_LeaseFailsOnAlreadyLeasedFileByOtherObject()
        {
            var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            FileBlob blob1 = new FileBlob(testFile.FullName);
            FileBlob blob2 = new FileBlob(testFile.FullName);
            var data = Encoding.UTF8.GetBytes("Hello, World!");
            blob1.Write(data);
            var leasePeriodMilliseconds = 10000;

            // Leased by another thread/process/object
            blob2.Lease(leasePeriodMilliseconds);

            // Lease should fail as already leased
            Assert.Null(blob1.Lease(10));

            // Clean up
            blob2.Delete();
        }

        [Fact]
        public void FileBlobTests_Delete()
        {
            var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            FileBlob blob = new FileBlob(testFile.FullName);

            blob.Delete();

            // Assert
            Assert.False(testFile.Exists);
        }

        [Fact(Skip = "Unstable")]
        public void FileBlobTests_DeleteFailsAfterLeaseIsExpired()
        {
            var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

            // set maintenance job interval to 2 secs
            using var storage = new FileStorage(testDirectory.FullName, 10, 2);

            var data = Encoding.UTF8.GetBytes("Hello, World!");

            var blob = storage.CreateBlob(data);

            var leasePeriodMilliseconds = 1;

            // lease for 1 ms
            blob.Lease(leasePeriodMilliseconds);

            // Wait for lease to expire and maintenance job to run
            Thread.Sleep(5000);

            blob.Delete();

            // Assert
            Assert.NotNull(storage.GetBlob());

            testDirectory.Delete(true);
        }

        [Fact(Skip = "Unstable")]
        public void FileBlobTests_LeaseTimeIsUpdatedWhenLeasingAlreadyLeasedFile()
        {
            var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            FileBlob blob = new FileBlob(testFile.FullName);
            var data = Encoding.UTF8.GetBytes("Hello, World!");

            blob.Write(data);

            var leasePeriodMilliseconds = 10000;
            blob.Lease(leasePeriodMilliseconds);

            var leaseTime = PersistentStorageHelper.GetDateTimeFromLeaseName(blob.FullPath);

            Assert.NotNull(blob.Lease(10000));

            var newLeaseTime = PersistentStorageHelper.GetDateTimeFromLeaseName(blob.FullPath);

            Assert.NotEqual(leaseTime, newLeaseTime);

            blob.Delete();
        }

        [Fact]
        public void FileBlobTests_FailedWrite()
        {
            var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            FileBlob blob = new FileBlob(testFile.FullName);

            Assert.Null(blob.Write(null));
        }
    }
}
