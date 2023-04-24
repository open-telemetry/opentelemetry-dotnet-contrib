// <copyright file="FileBlobProviderTests.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.PersistentStorage.FileSystem.Tests;

public class FileBlobProviderTests
{
    [Fact]
    public void FileBlobProvider_CreatesSubDirectory()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

        using var blobProvider = new FileBlobProvider(testDirectory.FullName);

        Assert.Equal(testDirectory.FullName, blobProvider.DirectoryPath);
        Directory.Exists(testDirectory.FullName);

        // clean up
        Directory.Delete(testDirectory.FullName, true);
    }

    [Fact]
    public void FileBlobProvider_E2E_Test()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        using var blobProvider = new FileBlobProvider(testDirectory.FullName);

        var data = Encoding.UTF8.GetBytes("Hello, World!");

        // Create blob.
        Assert.True(blobProvider.TryCreateBlob(data, out var blob1));

        // Get blob.
        Assert.True(blobProvider.TryGetBlob(out var blob2));

        Assert.Single(blobProvider.GetBlobs());

        // Verify file name from both create blob and get blob are same.
        Assert.Equal(((FileBlob)blob1).FullPath, ((FileBlob)blob2).FullPath);

        // Validate if content in the blob is same as buffer data passed to create blob.
        Assert.True(blob1.TryRead(out var blobContent));
        Assert.Equal(data, blobContent);

        testDirectory.Delete(true);
    }

    [Fact]
    public void FileBlobProvider_CreateBlobReturnsNullIfBlobProviderIsFull()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        using var blobProvider = new FileBlobProvider(testDirectory.FullName, 100);

        // write a file to fill up the configured max space.
        Assert.True(blobProvider.TryCreateBlob(new byte[100], out _));

        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.False(blobProvider.TryCreateBlob(data, out var blob));
        Assert.Null(blob);

        testDirectory.Delete(true);
    }

    [Fact]
    public void FileBlobProvider_TestRetentionPeriod()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        long maxSizeInBytes = 100000;
        int maintenancePeriodInMilliseconds = 3000;
        int retentionPeriodInMilliseconds = 1000;
        int writeTimeOutInMilliseconds = 1000;
        using var blobProvider = new FileBlobProvider(
            testDirectory.FullName,
            maxSizeInBytes,
            maintenancePeriodInMilliseconds,
            retentionPeriodInMilliseconds,
            writeTimeOutInMilliseconds);

        var data = Encoding.UTF8.GetBytes("Hello, World!");
        Assert.True(blobProvider.TryCreateBlob(data, out var blob));

        // Wait for rentention deadline to expire
        Thread.Sleep(2000);
        var retentionDeadline = DateTime.UtcNow - TimeSpan.FromMilliseconds(retentionPeriodInMilliseconds);
        PersistentStorageHelper.RemoveExpiredBlob(retentionDeadline, ((FileBlob)blob).FullPath);

        // Blob will be deleted as retention period is 1 sec
        Assert.False(File.Exists(((FileBlob)blob).FullPath));

        testDirectory.Delete(true);
    }

    [Fact]
    public void FileBlobProvider_TestWriteTimeoutPeriod()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        long maxSizeInBytes = 100000;
        int maintenancePeriodInMilliseconds = 3000;
        int retentionPeriodInMilliseconds = 2000;
        int writeTimeOutInMilliseconds = 1000;
        using var blobProvider = new FileBlobProvider(
            testDirectory.FullName,
            maxSizeInBytes,
            maintenancePeriodInMilliseconds,
            retentionPeriodInMilliseconds,
            writeTimeOutInMilliseconds);

        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.True(blobProvider.TryCreateBlob(data, out var blob));

        // Mock write
        File.Move(((FileBlob)blob).FullPath, ((FileBlob)blob).FullPath + ".tmp");

        // validate file moved successfully
        Assert.True(File.Exists(((FileBlob)blob).FullPath + ".tmp"));

        // wait for timeout period
        Thread.Sleep(2000);
        var timeoutDeadline = DateTime.UtcNow - TimeSpan.FromMilliseconds(writeTimeOutInMilliseconds);
        PersistentStorageHelper.RemoveTimedOutTmpFiles(timeoutDeadline, ((FileBlob)blob).FullPath + ".tmp");

        // tmp file will be deleted as write timeout period is 1 sec
        Assert.False(File.Exists(((FileBlob)blob).FullPath + ".tmp"));
        Assert.False(File.Exists(((FileBlob)blob).FullPath));

        testDirectory.Delete(true);
    }

    [Fact]
    public void FileBlobProviderTests_TestLeaseExpiration()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        long maxSizeInBytes = 100000;
        int maintenancePeriodInMilliseconds = 3000;
        int retentionPeriodInMilliseconds = 2000;
        int writeTimeOutInMilliseconds = 1000;
        using var blobProvider = new FileBlobProvider(
            testDirectory.FullName,
            maxSizeInBytes,
            maintenancePeriodInMilliseconds,
            retentionPeriodInMilliseconds,
            writeTimeOutInMilliseconds);

        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.True(blobProvider.TryCreateBlob(data, out var blob));
        var blobPath = ((FileBlob)blob).FullPath;

        blob.TryLease(1000);
        var leasePath = ((FileBlob)blob).FullPath;
        Assert.True(File.Exists(leasePath));

        // Wait for lease to expire
        Thread.Sleep(2000);
        PersistentStorageHelper.RemoveExpiredLease(DateTime.UtcNow, leasePath);

        // File name will be change to .blob
        Assert.True(File.Exists(blobPath));
        Assert.False(File.Exists(leasePath));

        testDirectory.Delete(true);
    }
}
