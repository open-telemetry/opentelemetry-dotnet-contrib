# Persistent Storage for OpenTelemetry Exporters

This package enables OpenTelemetry exporters to store telemetry offline in case
of transient failures. The data can be read at later time for retries.

## Installation

TODO

## Basic Usage

### Setup Storage

```csharp
var testDir = new DirectoryInfo(Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData)
            , "test"));
using var storage = new LocalFileStorage(testDir.FullName);
```

Following is the complete list of configurable options that can be used to set
up storage:

`path`: Sets file storage folder location where blobs are stored.

`maxSizeInBytes` : Maximum allowed storage folder size. Default is 50 MB.

`maintenancePeriodInMilliseconds`: Maintenance event runs at specified interval.
Default is 2 minutes. Maintenance event performs following 3 tasks:

1. Removes `*.blob` files for which the retention period has expired.
2. Removes `*.tmp` files for which the write timeout period has expired.
3. Updates `*.lock` files to `*.blob` for which the lease period has expired.

`retentionPeriodInMilliseconds` : Retention period in milliseconds for the blob.
Default is 2 days.

`writeTimeoutInMilliseconds`: Controls the timeout when writing a buffer to
blob. Default is 1 minute.

### CreateBlob

`CreateBlob(byte[] buffer, int leasePeriodMilliseconds = 0)` method can be used
to store data on disk in case of failures. The file stored will have `.blob`
extension.

```csharp
IPersistentBlob blob = storage.CreateBlob(data);

// Create blob and acquire lease
IPersistentBlob blob = storage.CreateBlob(data, 10);
```

### GetBlob and GetBlobs

`GetBlob` can be used to read single blob or `GetBlobs` can be used to get list
of all blobs stored on disk. The result will only include files with `.blob`
extension.

```csharp
// Get single blob from storage.
IPersistentBlob blob1 = storage.GetBlob();

// List all blobs.
foreach (var blobItem in storage.GetBlobs())
{
    Console.WriteLine(((LocalFileBlob)blobItem).FullPath);
}
```

### Lease

When reading data back from disk, `Lease(int leasePeriodMilliseconds)` method
should be used first to acquire lease on blob. This prevents it to be read
concurrently, until the lease period expires. Leasing a blob changes the file
extension to `.lock`.

```csharp
IPersistentBlob blob2 = blob1.Lease(10);
```

### Read

Once the lease is acquired on the blob, the data can be read using `Read`
method.

```csharp
byte[] data =  blob2.Read();
```

### Delete

`Delete` method can be used to delete the blob.

```csharp
blob2.Delete();
```

## Sample

```c#
var testDir = new DirectoryInfo(Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData)
            , "test"));
using var storage = new LocalFileStorage(testDir.FullName);

var data = Encoding.UTF8.GetBytes("Hello, World!");

// Create blob.
IPersistentBlob blob1 = storage.CreateBlob(data);

// List all blobs.
foreach (var blobItem in storage.GetBlobs())
{
    Console.WriteLine(((LocalFileBlob)blobItem).FullPath);
}

// Get blob.
IPersistentBlob blob2 = storage.GetBlob();

if(blob2 != null)
{
    // Lease before reading
    IPersistentBlob blob3 = blob2.Lease(10);

    // Check if the lease is acquired
    if (blob3 != null)
    {
        // Read
        System.Text.Encoding.UTF8.GetString(blob3.Read());

        // Delete
        blob3.Delete();
    }
}
```
