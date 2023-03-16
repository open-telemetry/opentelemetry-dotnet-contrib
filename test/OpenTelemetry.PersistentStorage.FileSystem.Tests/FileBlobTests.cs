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
using OpenTelemetry.PersistentStorage.Abstractions;
using Xunit;

namespace OpenTelemetry.PersistentStorage.FileSystem.Tests;

public class FileBlobTests
{
    [Fact]
    public void FileBlobTests_E2E_Test()
    {
        var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        PersistentBlob blob = new FileBlob(testFile.FullName);

        var data = Encoding.UTF8.GetBytes("Hello, World!");
        blob.TryWrite(data);
        blob.TryRead(out var blobContent);

        Assert.Equal(testFile.FullName, ((FileBlob)blob).FullPath);
        Assert.Equal(data, blobContent);

        Assert.True(blob.TryDelete());
        Assert.False(testFile.Exists);
    }

    [Fact]
    public void FileBlobTests_Lease()
    {
        var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        PersistentBlob blob = new FileBlob(testFile.FullName);

        var data = Encoding.UTF8.GetBytes("Hello, World!");
        var leasePeriodMilliseconds = 1000;
        blob.TryWrite(data);
        blob.TryLease(leasePeriodMilliseconds);

        Assert.Contains(".lock", ((FileBlob)blob).FullPath);

        Assert.True(blob.TryDelete());
        Assert.False(testFile.Exists);
    }

    [Fact]
    public void FileBlobTests_LeaseAfterDelete()
    {
        var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        PersistentBlob blob = new FileBlob(testFile.FullName);

        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.True(blob.TryWrite(data));
        Assert.True(blob.TryDelete());

        // Lease should return false
        Assert.False(blob.TryLease(1000));
    }

    [Fact]
    public void FileBlobTests_ReadFailsOnAlreadyLeasedFile()
    {
        var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        FileBlob blob1 = new FileBlob(testFile.FullName);
        FileBlob blob2 = new FileBlob(testFile.FullName);
        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.True(blob2.TryWrite(data));

        // Leased by another thread/process/object
        Assert.True(blob2.TryLease(10000));

        // Read should fail as file is leased
        Assert.False(blob1.TryRead(out var blob));
        Assert.Null(blob);

        // Clean up
        Assert.True(blob2.TryDelete());
    }

    [Fact]
    public void FileBlobTests_LeaseFailsOnAlreadyLeasedFileByOtherObject()
    {
        var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        FileBlob blob1 = new FileBlob(testFile.FullName);
        FileBlob blob2 = new FileBlob(testFile.FullName);
        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.True(blob1.TryWrite(data));

        // Leased by another thread/process/object
        Assert.True(blob2.TryLease(10000));

        // Lease should fail as already leased
        Assert.False(blob1.TryLease(10));

        // Clean up
        Assert.True(blob2.TryDelete());
    }

    [Fact]
    public void FileBlobTests_Delete()
    {
        var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        FileBlob blob = new FileBlob(testFile.FullName);

        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.True(blob.TryWrite(data));

        // Assert
        Assert.True(blob.TryDelete());
        Assert.False(testFile.Exists);
    }

    [Fact(Skip = "Unstable")]
    public void FileBlobTests_DeleteFailsAfterLeaseIsExpired()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

        // set maintenance job interval to 2 secs
        using var storage = new FileBlobProvider(testDirectory.FullName, 10, 2);

        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.True(storage.TryCreateBlob(data, out var blob));

        var leasePeriodMilliseconds = 1;

        // lease for 1 ms
        blob.TryLease(leasePeriodMilliseconds);

        // Wait for lease to expire and maintenance job to run
        Thread.Sleep(5000);

        blob.TryDelete();

        // Assert
        Assert.True(storage.TryGetBlob(out var outputBlob));
        Assert.NotNull(outputBlob);

        testDirectory.Delete(true);
    }

    [Fact(Skip = "Unstable")]
    public void FileBlobTests_LeaseTimeIsUpdatedWhenLeasingAlreadyLeasedFile()
    {
        var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        FileBlob blob = new FileBlob(testFile.FullName);
        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.True(blob.TryWrite(data));

        var leasePeriodMilliseconds = 10000;
        Assert.True(blob.TryLease(leasePeriodMilliseconds));

        var leaseTime = PersistentStorageHelper.GetDateTimeFromLeaseName(blob.FullPath);

        Assert.True(blob.TryLease(leasePeriodMilliseconds));

        var newLeaseTime = PersistentStorageHelper.GetDateTimeFromLeaseName(blob.FullPath);

        Assert.NotEqual(leaseTime, newLeaseTime);

        Assert.True(blob.TryDelete());
    }

    [Fact]
    public void FileBlobTests_FailedWrite()
    {
        var nonExistingPath = Path.Combine("FakePath:/", Path.GetRandomFileName());
        FileBlob blob = new FileBlob(nonExistingPath);

        Assert.False(blob.TryWrite(Array.Empty<byte>()));
    }
}
