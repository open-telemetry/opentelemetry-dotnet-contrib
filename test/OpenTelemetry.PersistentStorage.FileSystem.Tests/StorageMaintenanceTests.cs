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

    [Theory]
    [InlineData("2020-01-01T000000.0000000Z-0123456789ABCDEF0123456789ABCDEF.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdeF.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789ABCDEF0123456789ABCDEF.blob.tmp")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789ABCDEF0123456789ABCDEF.blob@2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789ABCDEF0123456789ABCDEF.blob@2020-01-01T000000.0000000Z.lock.tmp")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789ABCDEF0123456789ABCDEF.blob@2020-01-01T000000.0000000Z.lock@2020-01-01T000000.0000000Z.lock")]
    public void MaintenanceDoesNotRemoveFilesWithUppercaseGuidsItDidNotCreate(string fileName)
    {
        // The component only creates files with GUIDs formatted as lowercase hexadecimal digits.
        var foreignFile = this.CreateFile(fileName);

        PersistentStorageHelper.RemoveExpiredBlobs(this.directory, DefaultRetentionMs, DefaultWriteTimeoutMs);

        Assert.True(File.Exists(foreignFile), $"{fileName} was removed or renamed.");
    }

    [Theory]
    [InlineData("2020-01-01T000000.0000000Z-0123456789ABCDEF0123456789ABCDEF.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdeF.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdeg.blob")]
    [InlineData("2020-01-01T000000.0000000Z-{0123456789abcdef0123456789abcde}.blob")]
    public void IsBlobFileNameRequiresLowercaseHexadecimalGuid(string fileName)
        => Assert.False(PersistentStorageHelper.IsBlobFileName(fileName));

    [Theory]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z.lock@notes.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@notes.lock@2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z.LOCK@2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z.lock@.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@@2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z.lock@2020-01-01T000000.0000000Z.lock.TMP")]
    [InlineData("worldmap.blob@2020-01-01T000000.0000000Z.lock@2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-notaguid.blob@2020-01-01T000000.0000000Z.lock@2020-01-01T000000.0000000Z.lock.tmp")]
    public void MaintenanceDoesNotRemoveNestedLeasesItDidNotCreate(string fileName)
    {
        var foreignFile = this.CreateFile(fileName);

        PersistentStorageHelper.RemoveExpiredBlobs(this.directory, DefaultRetentionMs, DefaultWriteTimeoutMs);

        Assert.True(File.Exists(foreignFile), $"{fileName} was removed or renamed.");
    }

    [Fact]
    public void MaintenanceReleasesExpiredNestedLeases()
    {
        // Writing to a blob that is already leased with a new lease appends a further lease to its name: {blob}@{timestamp}.lock@{timestamp}.lock
        var blob = OwnBlobName(DateTime.UtcNow);
        var expiredLease = this.CreateFile($"{blob}@2020-01-01T000000.0000000Z.lock@2020-01-01T000500.0000000Z.lock");
        var activeLease = this.CreateFile($"{blob}@2020-01-01T000000.0000000Z.lock@{FormatTimestamp(DateTime.UtcNow.AddHours(1))}.lock");

        PersistentStorageHelper.RemoveExpiredBlobs(this.directory, DefaultRetentionMs, DefaultWriteTimeoutMs);

        Assert.False(File.Exists(expiredLease), "The expired nested lease was not released.");
        Assert.True(File.Exists(Path.Combine(this.directory, $"{blob}@2020-01-01T000000.0000000Z.lock")), "The expired nested lease was not released to the lease it was derived from.");
        Assert.True(File.Exists(activeLease), "The active nested lease was released.");
    }

    [Fact]
    public void MaintenanceRemovesTimedOutTemporaryFilesOfNestedLeases()
    {
        var expiredLease = this.CreateFile(OwnBlobName(DateTime.UtcNow) + "@2020-01-01T000000.0000000Z.lock@2020-01-01T000500.0000000Z.lock.tmp");
        var activeLease = this.CreateFile(OwnBlobName(DateTime.UtcNow) + $"@2020-01-01T000000.0000000Z.lock@{FormatTimestamp(DateTime.UtcNow.AddHours(1))}.lock.tmp");

        PersistentStorageHelper.RemoveExpiredBlobs(this.directory, DefaultRetentionMs, DefaultWriteTimeoutMs);

        Assert.False(File.Exists(expiredLease), "The temporary file of an expired nested lease was not removed.");
        Assert.True(File.Exists(activeLease), "The temporary file of an active nested lease was removed.");
    }

    [Fact]
    public void WritingToLeasedBlobWithLeaseReplacesLease()
    {
        using var provider = new FileBlobProvider(this.directory);

        Assert.True(provider.TryCreateBlob([1, 2, 3], 60_000, out var blob));
        var leasePath = ((FileBlob)blob).FullPath;
        var blobPath = leasePath.Substring(0, leasePath.LastIndexOf('@'));

        Assert.True(blob.TryWrite([4, 5, 6], 60_000));
        var newLeasePath = ((FileBlob)blob).FullPath;

        // The blob is leased again from its original name, rather than the previous lease being extended,
        // and the previous lease is replaced rather than left alongside the newly written blob.
        Assert.StartsWith(blobPath + "@", newLeasePath, StringComparison.Ordinal);
        Assert.DoesNotContain(".lock@", newLeasePath, StringComparison.Ordinal);
        Assert.Equal([newLeasePath], Directory.GetFiles(this.directory));
        Assert.Equal([4, 5, 6], File.ReadAllBytes(newLeasePath));

        // The lease is kept while it is held, and released to the original name once it has expired.
        Assert.False(PersistentStorageHelper.RemoveExpiredLease(DateTime.UtcNow, newLeasePath));
        Assert.True(PersistentStorageHelper.RemoveExpiredLease(DateTime.UtcNow.AddMinutes(5), newLeasePath));

        Assert.Equal([blobPath], Directory.GetFiles(this.directory));
        Assert.Equal([4, 5, 6], File.ReadAllBytes(blobPath));
    }

    [Fact]
    public void WritingToLeasedBlobWithoutLeaseReleasesLease()
    {
        using var provider = new FileBlobProvider(this.directory);

        Assert.True(provider.TryCreateBlob([1, 2, 3], 60_000, out var blob));
        var leasePath = ((FileBlob)blob).FullPath;
        var blobPath = leasePath.Substring(0, leasePath.LastIndexOf('@'));

        Assert.True(blob.TryWrite([4, 5, 6]));

        Assert.Equal(blobPath, ((FileBlob)blob).FullPath);
        Assert.Equal([blobPath], Directory.GetFiles(this.directory));
        Assert.Equal([4, 5, 6], File.ReadAllBytes(blobPath));
    }

    [Fact]
    public void WritingToBlobWithoutLeaseReplacesBlob()
    {
        using var provider = new FileBlobProvider(this.directory);

        Assert.True(provider.TryCreateBlob([1, 2, 3], out var blob));
        var blobPath = ((FileBlob)blob).FullPath;

        Assert.True(blob.TryWrite([4, 5, 6]));

        Assert.Equal(blobPath, ((FileBlob)blob).FullPath);
        Assert.Equal([blobPath], Directory.GetFiles(this.directory));
        Assert.Equal([4, 5, 6], File.ReadAllBytes(blobPath));
    }

    [Fact]
    public void WritingToBlobWithLeaseReplacesBlob()
    {
        using var provider = new FileBlobProvider(this.directory);

        Assert.True(provider.TryCreateBlob([1, 2, 3], out var blob));
        var blobPath = ((FileBlob)blob).FullPath;

        Assert.True(blob.TryWrite([4, 5, 6], 60_000));
        var leasePath = ((FileBlob)blob).FullPath;

        Assert.StartsWith(blobPath + "@", leasePath, StringComparison.Ordinal);
        Assert.Equal([leasePath], Directory.GetFiles(this.directory));
        Assert.Equal([4, 5, 6], File.ReadAllBytes(leasePath));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60_000)]
    public void WritingToBlobWhoseLeaseWasReleasedReplacesBlob(int leasePeriodMilliseconds)
    {
        using var provider = new FileBlobProvider(this.directory, maxSizeInBytes: 10);

        Assert.True(provider.TryCreateBlob([1, 2, 3, 4], 60_000, out var blob));
        var leasePath = ((FileBlob)blob).FullPath;
        var blobPath = leasePath.Substring(0, leasePath.LastIndexOf('@'));

        // The lease expires and is released by the storage maintenance before the blob is written to again.
        Assert.True(PersistentStorageHelper.RemoveExpiredLease(DateTime.UtcNow.AddMinutes(5), leasePath));
        Assert.Equal([blobPath], Directory.GetFiles(this.directory));

        Assert.True(blob.TryWrite([5, 6, 7, 8], leasePeriodMilliseconds));

        var path = ((FileBlob)blob).FullPath;

        if (leasePeriodMilliseconds > 0)
        {
            Assert.StartsWith(blobPath + "@", path, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(blobPath, path);
        }

        Assert.Equal([path], Directory.GetFiles(this.directory));
        Assert.Equal([5, 6, 7, 8], File.ReadAllBytes(path));
        Assert.False(File.Exists(leasePath + ".tmp"));

        // Only the 4 bytes of the blob's current content are stored, so there is still space for another blob.
        Assert.True(provider.TryCreateBlob([9, 10, 11, 12], out _));
    }

    [Fact]
    public void RemoveReplacedFileRemovesLeasedFile()
    {
        var blobPath = Path.Combine(this.directory, OwnBlobName(DateTime.UtcNow));
        var leasePath = this.CreateFile($"{Path.GetFileName(blobPath)}@{FormatTimestamp(DateTime.UtcNow.AddMinutes(1))}.lock");

        Assert.Equal(4, PersistentStorageHelper.RemoveReplacedFile(leasePath, $"{blobPath}@{FormatTimestamp(DateTime.UtcNow.AddMinutes(2))}.lock"));
        Assert.Empty(Directory.GetFiles(this.directory));
    }

    [Fact]
    public void RemoveReplacedFileRemovesFileWhoseLeaseWasReleased()
    {
        // The lease expired and was released by the storage maintenance after the blob's new content was moved into place.
        var blobPath = this.CreateFile(OwnBlobName(DateTime.UtcNow));
        var leasePath = $"{blobPath}@{FormatTimestamp(DateTime.UtcNow.AddMinutes(1))}.lock";

        Assert.Equal(4, PersistentStorageHelper.RemoveReplacedFile(leasePath, $"{blobPath}@{FormatTimestamp(DateTime.UtcNow.AddMinutes(2))}.lock"));
        Assert.Empty(Directory.GetFiles(this.directory));
    }

    [Fact]
    public void RemoveReplacedFileRemovesFileWhoseNestedLeaseWasPartiallyReleased()
    {
        // Previous versions nested leases when a leased blob was written to with a new lease, and the storage
        // maintenance releases the leases one at a time, so the file may have had only its last lease released.
        var blobPath = Path.Combine(this.directory, OwnBlobName(DateTime.UtcNow));
        var partiallyReleasedPath = this.CreateFile($"{Path.GetFileName(blobPath)}@2020-01-01T000000.0000000Z.lock");
        var nestedLeasePath = $"{partiallyReleasedPath}@2020-01-01T000500.0000000Z.lock";

        Assert.Equal(4, PersistentStorageHelper.RemoveReplacedFile(nestedLeasePath, $"{blobPath}@{FormatTimestamp(DateTime.UtcNow.AddMinutes(2))}.lock"));
        Assert.Empty(Directory.GetFiles(this.directory));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60_000)]
    public void WritingToBlobWhoseNestedLeaseWasPartiallyReleasedReplacesBlob(int leasePeriodMilliseconds)
    {
        var blobPath = Path.Combine(this.directory, OwnBlobName(DateTime.UtcNow));
        var partiallyReleasedPath = this.CreateFile($"{Path.GetFileName(blobPath)}@2020-01-01T000000.0000000Z.lock");
        var nestedLeasePath = $"{partiallyReleasedPath}@2020-01-01T000500.0000000Z.lock";

        var blob = new FileBlob(nestedLeasePath);

        Assert.True(blob.TryWrite([5, 6, 7, 8], leasePeriodMilliseconds));

        var path = ((FileBlob)blob).FullPath;

        Assert.Equal([path], Directory.GetFiles(this.directory));
        Assert.Equal([5, 6, 7, 8], File.ReadAllBytes(path));
    }

    [Fact]
    public void RemoveReplacedFileDoesNotRemoveNewContentIfReplacedFileWasRemoved()
    {
        // A leased blob was written to without a lease, so its new content was moved to its name without the lease.
        // If the leased file has since been removed, it cannot have been released to that name as the new content
        // was already there, so the file at that name must not be removed as it is the content that was just written.
        var blobPath = this.CreateFile(OwnBlobName(DateTime.UtcNow));
        var leasePath = $"{blobPath}@{FormatTimestamp(DateTime.UtcNow.AddMinutes(1))}.lock";

        Assert.Equal(0, PersistentStorageHelper.RemoveReplacedFile(leasePath, blobPath));
        Assert.Equal([blobPath], Directory.GetFiles(this.directory));
    }

    [Fact]
    public void RemoveReplacedFileReturnsZeroIfFileDoesNotExist()
    {
        var blobPath = Path.Combine(this.directory, OwnBlobName(DateTime.UtcNow));
        var leasePath = $"{blobPath}@{FormatTimestamp(DateTime.UtcNow.AddMinutes(1))}.lock";

        Assert.Equal(0, PersistentStorageHelper.RemoveReplacedFile(leasePath, $"{blobPath}@{FormatTimestamp(DateTime.UtcNow.AddMinutes(2))}.lock"));
        Assert.Equal(0, PersistentStorageHelper.RemoveReplacedFile(blobPath, $"{blobPath}@{FormatTimestamp(DateTime.UtcNow.AddMinutes(2))}.lock"));
    }

    [Fact]
    public void WritingToExistingBlobDoesNotCountReplacedContentTowardsStorageLimit()
    {
        using var provider = new FileBlobProvider(this.directory, maxSizeInBytes: 10);

        Assert.True(provider.TryCreateBlob([1, 2, 3, 4], 60_000, out var blob));
        Assert.True(blob.TryWrite([5, 6, 7, 8], 60_000));
        Assert.True(blob.TryWrite([9, 10, 11, 12], 60_000));

        // Only the 4 bytes of the blob's current content are stored, so there is still space for another blob.
        Assert.True(provider.TryCreateBlob([13, 14, 15, 16], out _));
    }

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
    public void MaintenanceRemovesTemporaryFileLeftByFailedWriteToExistingBlob()
    {
        // A blob created a long time ago, whose lease has also long since expired
        var blobPath = Path.Combine(this.directory, OwnBlobName(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        var leasePath = this.CreateFile($"{Path.GetFileName(blobPath)}@2020-01-01T000500.0000000Z.lock");

        var blob = new FileBlob(leasePath);

        // Writing to the leased blob without a lease moves it back to its original name. If a file with that name
        // has since been created, the move fails, so the temporary file is left behind.
        File.WriteAllBytes(blobPath, [7, 8, 9]);
        Assert.False(blob.TryWrite([4, 5, 6]));

        var temporaryFile = Assert.Single(Directory.GetFiles(this.directory, "*.tmp"));

        // The temporary file is not timed out until the write timeout has elapsed since it was written, regardless of
        // when the blob was created or its lease expired, so that it is not removed while the write is in progress...
        var timeoutDeadline = DateTime.UtcNow - TimeSpan.FromMilliseconds(DefaultWriteTimeoutMs);
        Assert.False(PersistentStorageHelper.RemoveTimedOutTmpFiles(timeoutDeadline, temporaryFile));
        Assert.True(File.Exists(temporaryFile));

        // ...and it is removed once it has.
        Assert.True(PersistentStorageHelper.RemoveTimedOutTmpFiles(DateTime.UtcNow.AddMinutes(5), temporaryFile));
        Assert.False(File.Exists(temporaryFile));

        Assert.Equal("data", File.ReadAllText(leasePath));
        Assert.Equal([7, 8, 9], File.ReadAllBytes(blobPath));
    }

    [Fact]
    public void LeasesBeforeTheYear2000AreNotTreatedAsLegacy()
    {
        // Devices without a real-time clock may start with a clock in the past, such as 1970, until it is synchronized.
        var leasePath = this.CreateFile($"{OwnBlobName(new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc))}@1999-01-01T000500.0000000Z.lock");

        Assert.False(PersistentStorageHelper.RemoveExpiredLease(new DateTime(1999, 1, 1, 0, 1, 0, DateTimeKind.Utc), leasePath));
        Assert.True(File.Exists(leasePath));
    }

    [Theory]
    [InlineData("2567-02-29T000000.000000Z-0123456789abcdef0123456789abcdef.blob", false)]
    [InlineData("2567-02-29 000000.0000000Z-0123456789abcdef0123456789abcdef.blob", false)]
    [InlineData("2567-02-29T00:00:00.0000000Z-0123456789abcdef0123456789abcdef.blob", false)]
    [InlineData("2567-O2-29T000000.0000000Z-0123456789abcdef0123456789abcdef.blob", false)]
    [InlineData("\u0662\u0665\u0666\u0667-02-29T000000.0000000Z-0123456789abcdef0123456789abcdef.blob", false)]
    public void IsBlobFileNameAcceptsLegacyTimestampsWithTheSameFormat(string fileName, bool expected)
        => Assert.Equal(expected, PersistentStorageHelper.IsBlobFileName(fileName));

    [Fact]
    public void GetBlobsDoesNotReturnFilesItDidNotCreate()
    {
        var timestamp = FormatTimestamp(DateTime.UtcNow);

        this.CreateFile($"{timestamp}-report.blob");
        this.CreateFile($"{timestamp}-0123456789ABCDEF0123456789ABCDEF.blob");
        this.CreateFile($"{timestamp}- 0123456789abcdef0123456789abcdef.blob");

        this.CreateFile($"{timestamp}-00112233445566778899aabbccddeeff.BLOB");

        var ownBlob = this.CreateFile($"{timestamp}-fedcba9876543210fedcba9876543210.blob");

        using var provider = new FileBlobProvider(this.directory);

        var blob = Assert.Single(provider.GetBlobs());
        Assert.Equal(ownBlob, ((FileBlob)blob).FullPath);
    }

    [Fact]
    public void GetBlobsDoesNotReturnFilesWithDifferentlyCasedExtensionItDidNotCreate()
    {
        // On Windows, enumerating *.blob also matches files with differently cased extensions
        this.CreateFile($"{FormatTimestamp(DateTime.UtcNow)}-0123456789abcdef0123456789abcdef.BLOB");

        using var provider = new FileBlobProvider(this.directory);

        Assert.Empty(provider.GetBlobs());
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
