// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace OpenTelemetry.Instrumentation.SshNet.Implementation;

/// <summary>
/// An <see cref="ISftpClient"/> decorator that emits a span and a duration
/// measurement for every remote operation.
/// </summary>
/// <remarks>
/// Methods returning a stream, reader or writer only cover opening the remote
/// file; subsequent reads and writes are not traced. The legacy APM
/// (<c>Begin*</c>/<c>End*</c>) methods are passed through uninstrumented.
/// </remarks>
/// <param name="inner">The client to decorate.</param>
/// <param name="options">The instrumentation options.</param>
internal sealed class InstrumentedSftpClient(ISftpClient inner, SshNetInstrumentationOptions options)
    : InstrumentedBaseClient<ISftpClient>(inner, options, SshNetTelemetry.ProtocolSftp), ISftpClient
{
    public uint BufferSize
    {
        get => this.Inner.BufferSize;
        set => this.Inner.BufferSize = value;
    }

    public TimeSpan OperationTimeout
    {
        get => this.Inner.OperationTimeout;
        set => this.Inner.OperationTimeout = value;
    }

    public int ProtocolVersion => this.Inner.ProtocolVersion;
    public string WorkingDirectory => this.Inner.WorkingDirectory;

    // Transfers
    public void UploadFile(Stream input, string path, Action<ulong>? uploadCallback = null) =>
        this.Transfer("UploadFile", path, input, () => this.Inner.UploadFile(input, path, uploadCallback));

    public void UploadFile(Stream input, string path, bool canOverride, Action<ulong>? uploadCallback = null) =>
        this.Transfer("UploadFile", path, input, () => this.Inner.UploadFile(input, path, canOverride, uploadCallback));

    public Task UploadFileAsync(Stream input, string path, CancellationToken cancellationToken = default) =>
        this.TransferAsync("UploadFile", path, input, () => this.Inner.UploadFileAsync(input, path, cancellationToken));

    public Task UploadFileAsync(Stream input, string path, IProgress<UploadFileProgressReport>? uploadProgress, CancellationToken cancellationToken = default) =>
        this.TransferAsync("UploadFile", path, input, () => this.Inner.UploadFileAsync(input, path, uploadProgress, cancellationToken));

    public Task UploadFileAsync(Stream input, string path, bool canOverride, IProgress<UploadFileProgressReport>? uploadProgress = null, CancellationToken cancellationToken = default) =>
        this.TransferAsync("UploadFile", path, input, () => this.Inner.UploadFileAsync(input, path, canOverride, uploadProgress, cancellationToken));

    public void DownloadFile(string path, Stream output, Action<ulong>? downloadCallback = null) =>
        this.Transfer("DownloadFile", path, output, () => this.Inner.DownloadFile(path, output, downloadCallback));

    public Task DownloadFileAsync(string path, Stream output, CancellationToken cancellationToken = default) =>
        this.TransferAsync("DownloadFile", path, output, () => this.Inner.DownloadFileAsync(path, output, cancellationToken));

    public Task DownloadFileAsync(string path, Stream output, IProgress<DownloadFileProgressReport>? downloadProgress, CancellationToken cancellationToken = default) =>
        this.TransferAsync("DownloadFile", path, output, () => this.Inner.DownloadFileAsync(path, output, downloadProgress, cancellationToken));

    public IEnumerable<FileInfo> SynchronizeDirectories(string sourcePath, string destinationPath, string searchPattern) =>
        this.Run("SynchronizeDirectories", destinationPath, () => this.Inner.SynchronizeDirectories(sourcePath, destinationPath, searchPattern));

    // Directories

    public void ChangeDirectory(string path) =>
        this.Run("ChangeDirectory", path, () => this.Inner.ChangeDirectory(path));

    public Task ChangeDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        this.RunAsync("ChangeDirectory", path, () => this.Inner.ChangeDirectoryAsync(path, cancellationToken));

    public void CreateDirectory(string path) =>
        this.Run("CreateDirectory", path, () => this.Inner.CreateDirectory(path));

    public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        this.RunAsync("CreateDirectory", path, () => this.Inner.CreateDirectoryAsync(path, cancellationToken));

    public void DeleteDirectory(string path) =>
        this.Run("DeleteDirectory", path, () => this.Inner.DeleteDirectory(path));

    public Task DeleteDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        this.RunAsync("DeleteDirectory", path, () => this.Inner.DeleteDirectoryAsync(path, cancellationToken));

    public IEnumerable<ISftpFile> ListDirectory(string path, Action<int>? listCallback = null) =>
        this.Run("ListDirectory", path, () => this.Inner.ListDirectory(path, listCallback));

    public async IAsyncEnumerable<ISftpFile> ListDirectoryAsync(string path, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // The span covers the whole enumeration, not just the first page.
        using var scope = this.StartScope("ListDirectory", path);
        var enumerator = this.Inner.ListDirectoryAsync(path, cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                ISftpFile current;
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        break;
                    }

                    current = enumerator.Current;
                }
                catch (Exception ex)
                {
                    scope.SetException(ex);
                    throw;
                }

                yield return current;
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    // Files & metadata

    public void Delete(string path) =>
        this.Run("Delete", path, () => this.Inner.Delete(path));

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default) =>
        this.RunAsync("Delete", path, () => this.Inner.DeleteAsync(path, cancellationToken));

    public void DeleteFile(string path) =>
        this.Run("DeleteFile", path, () => this.Inner.DeleteFile(path));

    public Task DeleteFileAsync(string path, CancellationToken cancellationToken) =>
        this.RunAsync("DeleteFile", path, () => this.Inner.DeleteFileAsync(path, cancellationToken));

    public bool Exists(string path) =>
        this.Run("Exists", path, () => this.Inner.Exists(path));

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default) =>
        this.RunAsync("Exists", path, () => this.Inner.ExistsAsync(path, cancellationToken));

    public ISftpFile Get(string path) =>
        this.Run("Get", path, () => this.Inner.Get(path));

    public Task<ISftpFile> GetAsync(string path, CancellationToken cancellationToken) =>
        this.RunAsync("Get", path, () => this.Inner.GetAsync(path, cancellationToken));

    public SftpFileAttributes GetAttributes(string path) =>
        this.Run("GetAttributes", path, () => this.Inner.GetAttributes(path));

    public Task<SftpFileAttributes> GetAttributesAsync(string path, CancellationToken cancellationToken) =>
        this.RunAsync("GetAttributes", path, () => this.Inner.GetAttributesAsync(path, cancellationToken));

    public void SetAttributes(string path, SftpFileAttributes fileAttributes) =>
        this.Run("SetAttributes", path, () => this.Inner.SetAttributes(path, fileAttributes));

    public SftpFileSystemInformation GetStatus(string path) =>
        this.Run("GetStatus", path, () => this.Inner.GetStatus(path));

    public Task<SftpFileSystemInformation> GetStatusAsync(string path, CancellationToken cancellationToken) =>
        this.RunAsync("GetStatus", path, () => this.Inner.GetStatusAsync(path, cancellationToken));

    public void ChangePermissions(string path, short mode) =>
        this.Run("ChangePermissions", path, () => this.Inner.ChangePermissions(path, mode));

    public void RenameFile(string oldPath, string newPath) =>
        this.Run("RenameFile", oldPath, () => this.Inner.RenameFile(oldPath, newPath), newPath);

    public void RenameFile(string oldPath, string newPath, bool isPosix) =>
        this.Run("RenameFile", oldPath, () => this.Inner.RenameFile(oldPath, newPath, isPosix), newPath);

    public Task RenameFileAsync(string oldPath, string newPath, CancellationToken cancellationToken) =>
        this.RunAsync("RenameFile", oldPath, () => this.Inner.RenameFileAsync(oldPath, newPath, cancellationToken), newPath);

    public void SymbolicLink(string path, string linkPath) =>
        this.Run("SymbolicLink", path, () => this.Inner.SymbolicLink(path, linkPath), linkPath);

    public DateTime GetLastAccessTime(string path) =>
        this.Run("GetAttributes", path, () => this.Inner.GetLastAccessTime(path));

    public DateTime GetLastAccessTimeUtc(string path) =>
        this.Run("GetAttributes", path, () => this.Inner.GetLastAccessTimeUtc(path));

    public DateTime GetLastWriteTime(string path) =>
        this.Run("GetAttributes", path, () => this.Inner.GetLastWriteTime(path));

    public DateTime GetLastWriteTimeUtc(string path) =>
        this.Run("GetAttributes", path, () => this.Inner.GetLastWriteTimeUtc(path));

    public void SetLastAccessTime(string path, DateTime lastAccessTime) =>
        this.Run("SetAttributes", path, () => this.Inner.SetLastAccessTime(path, lastAccessTime));

    public void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc) =>
        this.Run("SetAttributes", path, () => this.Inner.SetLastAccessTimeUtc(path, lastAccessTimeUtc));

    public void SetLastWriteTime(string path, DateTime lastWriteTime) =>
        this.Run("SetAttributes", path, () => this.Inner.SetLastWriteTime(path, lastWriteTime));

    public void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc) =>
        this.Run("SetAttributes", path, () => this.Inner.SetLastWriteTimeUtc(path, lastWriteTimeUtc));

    // Whole-file convenience methods

    public void AppendAllLines(string path, IEnumerable<string> contents) =>
        this.Run("AppendAllLines", path, () => this.Inner.AppendAllLines(path, contents));

    public void AppendAllLines(string path, IEnumerable<string> contents, Encoding encoding) =>
        this.Run("AppendAllLines", path, () => this.Inner.AppendAllLines(path, contents, encoding));

    public void AppendAllText(string path, string contents) =>
        this.Run("AppendAllText", path, () => this.Inner.AppendAllText(path, contents));

    public void AppendAllText(string path, string contents, Encoding encoding) =>
        this.Run("AppendAllText", path, () => this.Inner.AppendAllText(path, contents, encoding));

    public byte[] ReadAllBytes(string path) =>
        this.Run("ReadAllBytes", path, () => this.Inner.ReadAllBytes(path));

    public string[] ReadAllLines(string path) =>
        this.Run("ReadAllLines", path, () => this.Inner.ReadAllLines(path));

    public string[] ReadAllLines(string path, Encoding encoding) =>
        this.Run("ReadAllLines", path, () => this.Inner.ReadAllLines(path, encoding));

    public string ReadAllText(string path) =>
        this.Run("ReadAllText", path, () => this.Inner.ReadAllText(path));

    public string ReadAllText(string path, Encoding encoding) =>
        this.Run("ReadAllText", path, () => this.Inner.ReadAllText(path, encoding));

    public IEnumerable<string> ReadLines(string path) =>
        this.Run("ReadLines", path, () => this.Inner.ReadLines(path));

    public IEnumerable<string> ReadLines(string path, Encoding encoding) =>
        this.Run("ReadLines", path, () => this.Inner.ReadLines(path, encoding));

    public void WriteAllBytes(string path, byte[] bytes) =>
        this.Run("WriteAllBytes", path, () => this.Inner.WriteAllBytes(path, bytes));

    public void WriteAllLines(string path, IEnumerable<string> contents) =>
        this.Run("WriteAllLines", path, () => this.Inner.WriteAllLines(path, contents));

    public void WriteAllLines(string path, IEnumerable<string> contents, Encoding encoding) =>
        this.Run("WriteAllLines", path, () => this.Inner.WriteAllLines(path, contents, encoding));

    public void WriteAllLines(string path, string[] contents) =>
        this.Run("WriteAllLines", path, () => this.Inner.WriteAllLines(path, contents));

    public void WriteAllLines(string path, string[] contents, Encoding encoding) =>
        this.Run("WriteAllLines", path, () => this.Inner.WriteAllLines(path, contents, encoding));

    public void WriteAllText(string path, string contents) =>
        this.Run("WriteAllText", path, () => this.Inner.WriteAllText(path, contents));

    public void WriteAllText(string path, string contents, Encoding encoding) =>
        this.Run("WriteAllText", path, () => this.Inner.WriteAllText(path, contents, encoding));

    // Stream-returning methods: only opening the remote file is traced.

    public SftpFileStream Open(string path, FileMode mode) =>
        this.Run("Open", path, () => this.Inner.Open(path, mode));

    public SftpFileStream Open(string path, FileMode mode, FileAccess access) =>
        this.Run("Open", path, () => this.Inner.Open(path, mode, access));

    public Task<SftpFileStream> OpenAsync(string path, FileMode mode, FileAccess access, CancellationToken cancellationToken) =>
        this.RunAsync("Open", path, () => this.Inner.OpenAsync(path, mode, access, cancellationToken));

    public SftpFileStream OpenRead(string path) =>
        this.Run("Open", path, () => this.Inner.OpenRead(path));

    public SftpFileStream OpenWrite(string path) =>
        this.Run("Open", path, () => this.Inner.OpenWrite(path));

    public StreamReader OpenText(string path) =>
        this.Run("Open", path, () => this.Inner.OpenText(path));

    public SftpFileStream Create(string path) =>
        this.Run("Create", path, () => this.Inner.Create(path));

    public SftpFileStream Create(string path, int bufferSize) =>
        this.Run("Create", path, () => this.Inner.Create(path, bufferSize));

    public StreamWriter CreateText(string path) =>
        this.Run("Create", path, () => this.Inner.CreateText(path));

    public StreamWriter CreateText(string path, Encoding encoding) =>
        this.Run("Create", path, () => this.Inner.CreateText(path, encoding));

    public StreamWriter AppendText(string path) =>
        this.Run("Open", path, () => this.Inner.AppendText(path));

    public StreamWriter AppendText(string path, Encoding encoding) =>
        this.Run("Open", path, () => this.Inner.AppendText(path, encoding));

    // Legacy APM methods: passed through.

    public IAsyncResult BeginDownloadFile(string path, Stream output) =>
        this.Inner.BeginDownloadFile(path, output);

    public IAsyncResult BeginDownloadFile(string path, Stream output, AsyncCallback? asyncCallback) =>
        this.Inner.BeginDownloadFile(path, output, asyncCallback);

    public IAsyncResult BeginDownloadFile(string path, Stream output, AsyncCallback? asyncCallback, object? state, Action<ulong>? downloadCallback = null) =>
        this.Inner.BeginDownloadFile(path, output, asyncCallback, state, downloadCallback);

    public void EndDownloadFile(IAsyncResult asyncResult) =>
        this.Inner.EndDownloadFile(asyncResult);

    public IAsyncResult BeginListDirectory(string path, AsyncCallback? asyncCallback, object? state, Action<int>? listCallback = null) =>
        this.Inner.BeginListDirectory(path, asyncCallback, state, listCallback);

    public IEnumerable<ISftpFile> EndListDirectory(IAsyncResult asyncResult) =>
        this.Inner.EndListDirectory(asyncResult);

    public IAsyncResult BeginSynchronizeDirectories(string sourcePath, string destinationPath, string searchPattern, AsyncCallback? asyncCallback, object? state) =>
        this.Inner.BeginSynchronizeDirectories(sourcePath, destinationPath, searchPattern, asyncCallback, state);

    public IEnumerable<FileInfo> EndSynchronizeDirectories(IAsyncResult asyncResult) =>
        this.Inner.EndSynchronizeDirectories(asyncResult);

    public IAsyncResult BeginUploadFile(Stream input, string path) =>
        this.Inner.BeginUploadFile(input, path);

    public IAsyncResult BeginUploadFile(Stream input, string path, AsyncCallback? asyncCallback) =>
        this.Inner.BeginUploadFile(input, path, asyncCallback);

    public IAsyncResult BeginUploadFile(Stream input, string path, AsyncCallback? asyncCallback, object? state, Action<ulong>? uploadCallback = null) =>
        this.Inner.BeginUploadFile(input, path, asyncCallback, state, uploadCallback);

    public IAsyncResult BeginUploadFile(Stream input, string path, bool canOverride, AsyncCallback? asyncCallback, object? state, Action<ulong>? uploadCallback = null) =>
        this.Inner.BeginUploadFile(input, path, canOverride, asyncCallback, state, uploadCallback);

    public void EndUploadFile(IAsyncResult asyncResult) =>
        this.Inner.EndUploadFile(asyncResult);

    private static long? TryGetPosition(Stream stream)
    {
        try
        {
            return stream.CanSeek ? stream.Position : null;
        }
        catch (Exception ex) when (ex is NotSupportedException or ObjectDisposedException)
        {
            return null;
        }
    }

    private static void SetTransferredBytes(SshNetOperationScope scope, Stream stream, long? startPosition)
    {
        if (startPosition is { } start && TryGetPosition(stream) is { } end && end >= start)
        {
            scope.SetTag(SshNetTelemetry.AttributeTransferBytes, end - start);
        }
    }

    private void Transfer(string operation, string path, Stream stream, Action action)
    {
        using var scope = this.StartScope(operation, path);
        var start = TryGetPosition(stream);
        try
        {
            action();
        }
        catch (Exception ex)
        {
            scope.SetException(ex);
            throw;
        }

        SetTransferredBytes(scope, stream, start);
    }

    private async Task TransferAsync(string operation, string path, Stream stream, Func<Task> action)
    {
        using var scope = this.StartScope(operation, path);
        var start = TryGetPosition(stream);
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            scope.SetException(ex);
            throw;
        }

        SetTransferredBytes(scope, stream, start);
    }
}
