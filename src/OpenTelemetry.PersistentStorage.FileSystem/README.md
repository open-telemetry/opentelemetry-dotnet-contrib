# Persistent Storage file based implementation

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.PersistentStorage.FileSystem)](https://www.nuget.org/packages/OpenTelemetry.PersistentStorage.FileSystem)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.PersistentStorage.FileSystem)](https://www.nuget.org/packages/OpenTelemetry.PersistentStorage.FileSystem)

This package provides an implementation of
[persistent-storage-abstractions](../OpenTelemetry.PersistentStorage.Abstractions/README.md#Persistent-Storage-Abstractions)
based on local file system. This component can be used by OpenTelemetry
exporters to improve the reliability of data delivery.

## Installation

```shell
dotnet add package OpenTelemetry.PersistentStorage.FileSystem
```

## File Details

### File naming

Each call to [CreateBlob](#createblob) methods create a single file(blob) at the
configured [directory path](#path). Each file that is created has unique
name in the format `yyyy-MM-ddTHHmmss.fffffffZ-GUID`. The file extension depends
on the operation. When creating a blob, the file is stored with the `.blob`
extension. If a lease is acquired on an existing file or on the file being
created, the file extension is changed to `.lock`, along with the lease
expiration time appended to its name in the format
`@yyyy-MM-ddTHHmmss.fffffffZ`. The `.tmp` extension is used for files while data
writing is in process.

### File Access

Access to the stored files is controlled by the underlying file system
permissions set on the directory where the files are stored.

### Data format

The data contained within the file is stored in its original, unprocessed format
provided in the byte array.

### Data retention

A blob stored on disk persists until it is explicitly deleted using the
[TryDelete](#delete) operation or is removed during the [maintenance
job](#maintenanceperiodinmilliseconds) upon the expiration of its
[retention](#retentionperiodinmilliseconds) period.

## Usage

### Setup FileBlobProvider

```csharp
using var persistentBlobProvider = new FileBlobProvider("@C:\temp");
```

#### Configuration

Following is the complete list of configurable options that can be used to set
up FileBlobProvider:

##### path

Sets folder location where blobs are stored.

##### maxSizeInBytes

Maximum allowed folder size. Default is `50 MB`.

##### maintenancePeriodInMilliseconds

Maintenance event runs at specified interval. Default is `2 minutes`. During
this event, the following tasks are performed:

* Removes `*.blob` files for which the retention period has expired.
* Removes `*.tmp` files for which the write timeout period has expired.
* Updates `*.lock` files to `*.blob` for which the lease period has expired.
* Updates `directorySize`.

##### retentionPeriodInMilliseconds

Retention period in milliseconds for the blob. Default is `2 days`.

##### writeTimeoutInMilliseconds

Controls the timeout when writing a buffer to blob. Default is `1 minute`.

### Blob Operations

#### CreateBlob

`TryCreateBlob(byte[] buffer, out PersistentBlob blob)` or `TryCreateBlob(byte[]
buffer, int leasePeriodMilliseconds = 0, out PersistentBlob blob)` can be used
to create a blob (single file). The file stored will have `.blob`
extension. If acquiring lease, the file will have `.lock` extension.

```csharp
// Try create blob without acquiring lease
persistentBlobProvider.TryCreateBlob(data, out var blob);

// Try create blob and acquire lease
persistentBlobProvider.TryCreateBlob(data, 1000, out var blob);
```

#### GetBlob and GetBlobs

`TryGetBlob` can be used to read single blob or `GetBlobs` can be used to get list
of all blobs stored on disk. The result will only include files with `.blob`
extension.

```csharp
// Get single blob from storage.
persistentBlobProvider.GetBlob(out var blob);

// List all blobs.
foreach (var blobItem in persistentBlobProvider.GetBlobs())
{
    Console.WriteLine(((FileBlob)blobItem).FullPath);
}
```

#### Lease

When reading data back from disk, `TryLease(int leasePeriodMilliseconds)` method
should be used first to acquire lease on blob. This prevents it to be read
concurrently, until the lease period expires. Leasing a blob changes the file
extension to `.lock`.

```csharp
blob.TryLease(1000);
```

#### Read

Once the lease is acquired on the blob, the data can be read using
`TryRead(out var data)` method.

```csharp
blob.TryRead(out var data);
```

#### Delete

`TryDelete` method can be used to delete the blob.

```csharp
blob.TryDelete();
```

### Example

```csharp
using var persistentBlobProvider = new FileBlobProvider("@C:\temp");

var data = Encoding.UTF8.GetBytes("Hello, World!");

// Create blob.
persistentBlobProvider.TryCreateBlob(data, out var createdBlob);

// List all blobs.
foreach (var blobItem in persistentBlobProvider.GetBlobs())
{
    Console.WriteLine(((FileBlob)blobItem).FullPath);
}

// Get single blob.
if (persistentBlobProvider.TryGetBlob(out var blob))
{
    // Lease before reading
    if (blob.TryLease(1000))
    {
        // Read
        if (blob.TryRead(out var outputData))
        {
            Console.WriteLine(Encoding.UTF8.GetString(outputData));
        }

        // Delete
        if (blob.TryDelete())
        {
            Console.WriteLine("Successfully deleted the blob");
        }
    }
}
```
