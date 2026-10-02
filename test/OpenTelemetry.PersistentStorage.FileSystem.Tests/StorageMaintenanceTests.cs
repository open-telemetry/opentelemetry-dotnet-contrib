// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Runtime.InteropServices;
using OpenTelemetry.Tests;

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
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.BLOB")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.Blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob.TMP")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.BLOB.tmp")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z.LOCK")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.BLOB@2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z.lock.TMP")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z.LOCK.tmp")]
    public void MaintenanceDoesNotRemoveFilesWithDifferentlyCasedExtensionsItDidNotCreate(string fileName)
    {
        // The component only creates files with lowercase extensions.
        var foreignFile = this.CreateFile(fileName);

        PersistentStorageHelper.RemoveExpiredBlobs(this.directory, DefaultRetentionMs, DefaultWriteTimeoutMs);

        Assert.True(File.Exists(foreignFile), $"{fileName} was removed or renamed.");
    }

    [Theory]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.BLOB")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.Blob")]
    public void IsBlobFileNameRequiresLowercaseExtension(string fileName)
        => Assert.False(PersistentStorageHelper.IsBlobFileName(fileName));

    [Fact]
    public void MaintenanceRemovesTimedOutTemporaryFilesOfLeasedBlobs()
    {
        // A temporary file of a leased blob is named {blob}@{timestamp}.lock.tmp, and is only abandoned
        // once the lease has expired, regardless of when the blob itself was created.
        var expiredLease = this.CreateFile(OwnBlobName(DateTime.UtcNow) + "@2020-01-01T000000.0000000Z.lock.tmp");
        var activeLease = this.CreateFile(OwnBlobName(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)) + $"@{FormatTimestamp(DateTime.UtcNow.AddHours(1))}.lock.tmp");

        PersistentStorageHelper.RemoveExpiredBlobs(this.directory, DefaultRetentionMs, DefaultWriteTimeoutMs);

        Assert.False(File.Exists(expiredLease), "The temporary file of an expired lease was not removed.");
        Assert.True(File.Exists(activeLease), "The temporary file of an active lease was removed.");
    }

    [Fact]
    public void MaintenanceRemovesTemporaryFileLeftByFailedWriteToLeasedBlob()
    {
        using var provider = new FileBlobProvider(this.directory);

        Assert.True(provider.TryCreateBlob([1, 2, 3], out var blob));
        Assert.True(blob.TryLease(1_000));

        var leasePath = ((FileBlob)blob).FullPath;
        var temporaryFile = leasePath + ".tmp";

        // Moving the temporary file over the existing leased blob fails, so the temporary file is left behind.
        Assert.False(blob.TryWrite([4, 5, 6]));
        Assert.True(File.Exists(temporaryFile));

        // The temporary file is kept while the lease is held...
        Assert.False(PersistentStorageHelper.RemoveTimedOutTmpFiles(DateTime.UtcNow, temporaryFile));
        Assert.True(File.Exists(temporaryFile));

        // ...and removed once the lease has expired for longer than the write timeout.
        Assert.True(PersistentStorageHelper.RemoveTimedOutTmpFiles(DateTime.UtcNow.AddMinutes(5), temporaryFile));
        Assert.False(File.Exists(temporaryFile));
        Assert.True(File.Exists(leasePath));
    }

    [Fact]
    public void GetUniqueFileNameUsesInvariantCalendar()
    {
        string fileName = null!;
        DateTime before = default;
        DateTime after = default;

        using (CultureSwitcher.UseCulture("th-TH"))
        {
            before = DateTime.UtcNow;
            fileName = PersistentStorageHelper.GetUniqueFileName(".blob");
            after = DateTime.UtcNow;
        }

        Assert.True(PersistentStorageHelper.IsBlobFileName(fileName), $"{fileName} is not a valid blob name.");
        Assert.InRange(PersistentStorageHelper.GetDateTimeFromBlobName(fileName), before, after);
    }

    [Fact]
    public void LeaseFileNamesUseInvariantCalendar()
    {
        using var provider = new FileBlobProvider(this.directory);

        using (CultureSwitcher.UseCulture("th-TH"))
        {
            var before = DateTime.UtcNow;
            Assert.True(provider.TryCreateBlob([1, 2, 3], 60_000, out var blob));
            var after = DateTime.UtcNow;

            var leasePath = ((FileBlob)blob).FullPath;
            Assert.InRange(PersistentStorageHelper.GetDateTimeFromLeaseName(leasePath), before.AddMinutes(1), after.AddMinutes(1));

            before = DateTime.UtcNow;
            Assert.True(blob.TryLease(120_000));
            after = DateTime.UtcNow;

            leasePath = ((FileBlob)blob).FullPath;
            Assert.InRange(PersistentStorageHelper.GetDateTimeFromLeaseName(leasePath), before.AddMinutes(2), after.AddMinutes(2));

            // The lease is recognized as one created by the component, so is released once expired.
            Assert.True(PersistentStorageHelper.RemoveExpiredLease(DateTime.UtcNow.AddHours(1), leasePath));
        }
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
        => $"{FormatTimestamp(timestamp)}-{Guid.NewGuid():N}.blob";

    private static string FormatTimestamp(DateTime timestamp)
        => timestamp.ToString("yyyy-MM-ddTHHmmss.fffffffZ", CultureInfo.InvariantCulture);

    private string CreateFile(string fileName)
    {
        var path = Path.Combine(this.directory, fileName);
        File.WriteAllText(path, "data");
        return path;
    }
}
