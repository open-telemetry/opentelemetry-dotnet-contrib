// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace OpenTelemetry.PersistentStorage.FileSystem.Tests;

public sealed class StorageMaintenanceTests : IDisposable
{
    private const long DefaultRetentionMs = 172800000; // 2 days (FileBlobProvider default)
    private const long DefaultWriteTimeoutMs = 60000;   // 1 minute (FileBlobProvider default)

    private readonly string directory;

    public StorageMaintenanceTests()
    {
        this.directory = Path.Combine(Path.GetTempPath(), "otel-storage-maintenance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.directory);
    }

    public void Dispose() => Directory.Delete(this.directory, recursive: true);

    [Theory]
    [InlineData("worldmap.blob")]
    [InlineData("cache-v2.blob")]
    [InlineData("2020-01-01T000000.0000000Z-notaguid.blob")]
    [InlineData("important-notes.tmp")]
    [InlineData("2020-01-01T000000.0000000Z-notaguid.blob.tmp")]
    [InlineData("database.lock")]
    [InlineData("backup@2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@notes.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@.lock")]
    public void MaintenanceDoesNotRemoveFilesItDidNotCreate(string fileName)
    {
        var foreignFile = this.CreateFile(fileName);

        PersistentStorageHelper.RemoveExpiredBlobs(this.directory, DefaultRetentionMs, DefaultWriteTimeoutMs);

        Assert.True(File.Exists(foreignFile), $"{fileName} was removed.");
    }

    [Theory]
    [InlineData("2020-01-01T000000.0000000Z- 0123456789abcdef0123456789abcdef.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef .blob")]
    [InlineData("2020-01-01T000000.0000000Z- 0123456789abcdef0123456789abcdef .blob")]
    [InlineData(" 2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob")]
    [InlineData("2020-01-01T000000.0000000Z -0123456789abcdef0123456789abcdef.blob")]
    [InlineData("2020-01-01T000000.0000000Z- 0123456789abcdef0123456789abcdef.blob.tmp")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef .blob.tmp")]
    [InlineData("2020-01-01T000000.0000000Z- 0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef .blob@2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@ 2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z .lock")]
    public void MaintenanceDoesNotRemoveFilesWithWhitespacePaddedNamesItDidNotCreate(string fileName)
    {
        var foreignFile = this.CreateFile(fileName);

        PersistentStorageHelper.RemoveExpiredBlobs(this.directory, DefaultRetentionMs, DefaultWriteTimeoutMs);

        Assert.True(File.Exists(foreignFile), $"{fileName} was removed or renamed.");
    }

    [Theory]
    [InlineData("2020-01-01T000000.0000000Z- 0123456789abcdef0123456789abcdef.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef .blob")]
    [InlineData("2020-01-01T000000.0000000Z-\t0123456789abcdef0123456789abcdef.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef\t.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef\n.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcde.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef0.blob")]
    public void IsBlobFileNameRejectsGuidsThatAreNotExactly32HexCharacters(string fileName)
        => Assert.False(PersistentStorageHelper.IsBlobFileName(fileName));

    [Theory]
    [InlineData("foreign\\2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob")]
    [InlineData("foreign\\2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob.tmp")]
    [InlineData("foreign\\2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z.lock")]
    public void MaintenanceDoesNotRemoveFilesWithBackslashesInTheirNameItDidNotCreate(string fileName)
    {
        // A backslash is a valid file name character on non-Windows platforms, so must not be treated as a directory separator.
        Assert.SkipWhen(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Backslash is not a valid file name character on Windows.");

        var foreignFile = this.CreateFile(fileName);

        PersistentStorageHelper.RemoveExpiredBlobs(this.directory, DefaultRetentionMs, DefaultWriteTimeoutMs);

        Assert.True(File.Exists(foreignFile), $"{fileName} was removed.");
    }

    [Fact]
    public void MaintenanceRemovesItsOwnExpiredFiles()
    {
        var recentBlob = this.CreateFile(PersistentStorageHelper.GetUniqueFileName(".blob"));
        var expiredBlob = this.CreateFile(OwnBlobName(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        var timedOutTemporaryFile = this.CreateFile(OwnBlobName(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)) + ".tmp");

        var leasedBlob = Path.Combine(this.directory, OwnBlobName(DateTime.UtcNow));
        var expiredLease = this.CreateFile(Path.GetFileName(leasedBlob) + "@2020-01-01T000000.0000000Z.lock");

        var blobWithMalformedLease = Path.Combine(this.directory, OwnBlobName(DateTime.UtcNow));
        var malformedLease = this.CreateFile(Path.GetFileName(blobWithMalformedLease) + "@not-a-timestamp.lock");

        PersistentStorageHelper.RemoveExpiredBlobs(this.directory, DefaultRetentionMs, DefaultWriteTimeoutMs);

        Assert.True(File.Exists(recentBlob));
        Assert.False(File.Exists(expiredBlob));
        Assert.False(File.Exists(timedOutTemporaryFile));

        // Expired leases are released.
        Assert.False(File.Exists(expiredLease));
        Assert.True(File.Exists(leasedBlob));

        // A lease whose timestamp is not in the format the component uses was not created by it, so is left alone.
        Assert.True(File.Exists(malformedLease));
        Assert.False(File.Exists(blobWithMalformedLease));
    }

    [Fact]
    public void MaintenanceTimerDoesNotRemoveFilesItDidNotCreate()
    {
        var foreignBlob = this.CreateFile("someones-database.blob");
        var expiredBlob = this.CreateFile(OwnBlobName(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        using var provider = new FileBlobProvider(
            this.directory,
            maxSizeInBytes: 52428800,
            maintenancePeriodInMilliseconds: 100,
            retentionPeriodInMilliseconds: DefaultRetentionMs,
            writeTimeoutInMilliseconds: (int)DefaultWriteTimeoutMs);

        // The removal of the component's own expired blob shows that the maintenance timer has run.
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (File.Exists(expiredBlob) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(50);
        }

        Assert.False(File.Exists(expiredBlob), "The maintenance timer did not run.");
        Assert.True(File.Exists(foreignBlob), "The maintenance timer removed a file it did not create.");
    }

    private static string OwnBlobName(DateTime timestamp)
        => $"{timestamp:yyyy-MM-ddTHHmmss.fffffffZ}-{Guid.NewGuid():N}.blob";

    private string CreateFile(string fileName)
    {
        var path = Path.Combine(this.directory, fileName);
        File.WriteAllText(path, "data");
        return path;
    }
}
