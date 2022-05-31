# Persistent Storage file based implementation

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Extensions.PersistentStorage.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.PersistentStorage)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Extensions.PersistentStorage.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.PersistentStorage)

This package provides file based implementation of persistent storage
abstractions. It is an experimental component which can be used by OpenTelemetry
Exporters to provide reliable data delivery.

## Installation

```shell
dotnet add package OpenTelemetry.Extensions.PersistentStorage
```

## Basic Usage

### Setup FileBlobProvider

```csharp
using var fileBlobProvider = new FileBlobProvider("test");
```

Following is the complete list of configurable options that can be used to set
up BlobProvider:

* `path`: Sets folder location where blobs are stored.

* `maxSizeInBytes`: Maximum allowed folder size. Default is 50 MB.

* `maintenancePeriodInMilliseconds`: Maintenance event runs at specified interval.
Default is 2 minutes. Maintenance event performs the following tasks:

  * Removes `*.blob` files for which the retention period has expired.
  * Removes `*.tmp` files for which the write timeout period has expired.
  * Updates `*.lock` files to `*.blob` for which the lease period has expired.
  * Updates `directorySize`.

* `retentionPeriodInMilliseconds`: Retention period in milliseconds for the blob.
Default is 2 days.

* `writeTimeoutInMilliseconds`: Controls the timeout when writing a buffer to
blob. Default is 1 minute.

### CreateBlob

`TryCreateBlob(byte[] buffer, out PersistentBlob blob)` or `TryCreateBlob(byte[]
buffer, int leasePeriodMilliseconds = 0, out PersistentBlob blob)` method can be
used to store data on disk in case of failures. The file stored will have
`.blob` extension. If acquiring lease, the file will have `.lock` extension.

```csharp
// Try create blob without acquiring lease
fileBlobProvider.TryCreateBlob(data, out var blob);

// Try create blob and acquire lease
fileBlobProvider.TryCreateBlob(data, 1000, out var blob);
```

### GetBlob and GetBlobs

`TryGetBlob` can be used to read single blob or `GetBlobs` can be used to get list
of all blobs stored on disk. The result will only include files with `.blob`
extension.

```csharp
// Get single blob from storage.
fileBlobProvider.GetBlob(out var blob);

// List all blobs.
foreach (var blobItem in fileBlobProvider.GetBlobs())
{
    Console.WriteLine(((FileBlob)blobItem).FullPath);
}
```

### Lease

When reading data back from disk, `TryLease(int leasePeriodMilliseconds)` method
should be used first to acquire lease on blob. This prevents it to be read
concurrently, until the lease period expires. Leasing a blob changes the file
extension to `.lock`.

```csharp
blob.TryLease(1000);
```

### Read

Once the lease is acquired on the blob, the data can be read using `TryRead(out
var data)` method.

```csharp
blob.TryRead(out var data);
```

### Delete

`TryDelete` method can be used to delete the blob.

```csharp
blob.TryDelete();
```

## Example

```csharp
using var fileBlobProvider = new FileBlobProvider("test");

var data = Encoding.UTF8.GetBytes("Hello, World!");

// Create blob.
fileBlobProvider.TryCreateBlob(data, out var blob);

// List all blobs.
foreach (var blobItem in fileBlobProvider.GetBlobs())
{
    Console.WriteLine(((FileBlob)blobItem).FullPath);
}

// Get blob.
if(fileBlobProvider.TryGetBlob(out var blob))
{
    // Lease before reading
    if (blob.TryLease(1000))
    {
        // Read
        blob.TryRead(out var data);

        // Delete
        blob.TryDelete();
    }
}
```
