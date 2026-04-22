// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using OpenTelemetry.OpAmp.Client.Internal;

namespace OpenTelemetry.OpAmp.Client.Messages;

/// <summary>
/// Currently active configuration reference.
/// </summary>
public sealed class EffectiveConfigFile
{
    private const int DefaultMaxSize = 512 * 1024; // 512 KB

    /// <summary>
    /// Initializes a new instance of the <see cref="EffectiveConfigFile"/> class.
    /// </summary>
    /// <param name="content">File content.</param>
    /// <param name="contentType">File MIME Content-Type.</param>
    /// <param name="fileName">File name.</param>
    /// <remarks>
    /// This constructor does not enforce a maximum content size. Callers must bound <paramref name="content"/> themselves.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contentType"/> or <paramref name="fileName"/> is null.</exception>
    public EffectiveConfigFile(ReadOnlyMemory<byte> content, string contentType = "", string fileName = "")
    {
        // Protect against null here because these are optional in the OpAMP spec, but can only be empty, not null.
#if NET
        ArgumentNullException.ThrowIfNull(contentType);
        ArgumentNullException.ThrowIfNull(fileName);
#else
        if (contentType == null)
        {
            throw new ArgumentNullException(nameof(contentType));
        }

        if (fileName == null)
        {
            throw new ArgumentNullException(nameof(fileName));
        }
#endif

        this.Content = content;
        this.ContentType = contentType;
        this.FileName = fileName;
    }

    /// <summary>
    /// Gets the file content.
    /// </summary>
    public ReadOnlyMemory<byte> Content { get; }

    /// <summary>
    /// Gets the MIME Content-Type of the file.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Gets the file name.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Creates an <see cref="EffectiveConfigFile"/> instance from a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">A <see cref="Stream"/> containing the effective configuration file content.</param>
    /// <param name="contentType">The MIME Content-Type of the configuration file.</param>
    /// <param name="fileName">The reported file name.</param>
    /// <param name="maxBytes">Maximum allowed file size in bytes. Default is 512 KB.</param>
    /// <returns>An instance of <see cref="EffectiveConfigFile"/>.</returns>
    /// <remarks>
    /// <para>
    /// The entire content is transmitted as-is to the OpAMP server.
    /// Do not include file content that contains secrets (passwords, tokens, private keys) unless
    /// the transport is secure (e.g. with TLS) and the OpAMP server is fully trusted.
    /// </para>
    /// <para>
    /// When validating stream size for non-seekable streams, this method may consume up to one byte
    /// beyond <paramref name="maxBytes"/> before throwing an exception.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/>, <paramref name="contentType"/>, or <paramref name="fileName"/> are null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stream"/> does not support reading.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxBytes"/> is less than zero.</exception>
    /// <exception cref="InvalidDataException">Thrown when the stream length exceeds the specified <paramref name="maxBytes"/>.</exception>
    public static EffectiveConfigFile CreateFromStream(Stream stream, string contentType = "", string fileName = "", int maxBytes = DefaultMaxSize)
    {
        ValidateArguments(stream, contentType, fileName, maxBytes);
        CheckSeekableSize(stream, maxBytes);

        if (maxBytes == 0)
        {
            // For non-seekable streams that CheckSeekableSize skips, we must peek one byte to verify emptiness.
            // For seekable streams, CheckSeekableSize already confirmed remaining length is zero.
            if (!stream.CanSeek && stream.ReadByte() != -1)
            {
                ThrowSizeLimitExceeded(maxBytes);
            }

            return new EffectiveConfigFile(ReadOnlyMemory<byte>.Empty, contentType, fileName);
        }

        var buffer = ArrayPool<byte>.Shared.Rent(maxBytes);
        try
        {
            var totalBytesRead = 0;

            while (totalBytesRead < maxBytes)
            {
                var bytesRead = stream.Read(buffer, totalBytesRead, maxBytes - totalBytesRead);
                if (bytesRead == 0)
                {
                    break;
                }

                totalBytesRead += bytesRead;
            }

            if (totalBytesRead == maxBytes && stream.ReadByte() != -1)
            {
                ThrowSizeLimitExceeded(maxBytes);
            }

            return BuildResult(buffer, totalBytesRead, contentType, fileName);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    /// <summary>
    /// Creates an <see cref="EffectiveConfigFile"/> instance from a <see cref="Stream"/> asynchronously.
    /// </summary>
    /// <param name="stream">A <see cref="Stream"/> containing the effective configuration file content.</param>
    /// <param name="contentType">The MIME Content-Type of the configuration file.</param>
    /// <param name="fileName">The reported file name.</param>
    /// <param name="maxBytes">Maximum allowed file size in bytes. Default is 512 KB.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous read, containing the resulting <see cref="EffectiveConfigFile"/>.</returns>
    /// <remarks>
    /// <para>
    /// The entire content is transmitted as-is to the OpAMP server.
    /// Do not include file content that contains secrets (passwords, tokens, private keys) unless
    /// the transport is secure (e.g. with TLS) and the OpAMP server is fully trusted.
    /// </para>
    /// <para>
    /// When validating stream size for non-seekable streams, this method may consume up to one byte
    /// beyond <paramref name="maxBytes"/> before throwing an exception.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/>, <paramref name="contentType"/>, or <paramref name="fileName"/> are null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stream"/> does not support reading.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxBytes"/> is less than zero.</exception>
    /// <exception cref="InvalidDataException">Thrown when the stream length exceeds the specified <paramref name="maxBytes"/>.</exception>
    public static async Task<EffectiveConfigFile> CreateFromStreamAsync(Stream stream, string contentType = "", string fileName = "", int maxBytes = DefaultMaxSize, CancellationToken cancellationToken = default)
    {
        ValidateArguments(stream, contentType, fileName, maxBytes);
        CheckSeekableSize(stream, maxBytes);

        if (maxBytes == 0)
        {
            // For non-seekable streams that CheckSeekableSize skips, we must peek one byte to verify emptiness.
            // For seekable streams, CheckSeekableSize already confirmed remaining length is zero.
            if (!stream.CanSeek)
            {
                // Zero maxBytes is extremely unlikely to be used, so the cost of this extra allocation is acceptable as an edge case.
                var peekBuffer = new byte[1];
#if NET
                var peeked = await stream.ReadAsync(peekBuffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
#else
                var peeked = await stream.ReadAsync(peekBuffer, 0, 1, cancellationToken).ConfigureAwait(false);
#endif
                if (peeked > 0)
                {
                    ThrowSizeLimitExceeded(maxBytes);
                }
            }

            return new EffectiveConfigFile(ReadOnlyMemory<byte>.Empty, contentType, fileName);
        }

        var buffer = ArrayPool<byte>.Shared.Rent(maxBytes);
        try
        {
            var totalBytesRead = 0;

            while (totalBytesRead < maxBytes)
            {
#if NET
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(totalBytesRead, maxBytes - totalBytesRead), cancellationToken).ConfigureAwait(false);
#else
                var bytesRead = await stream.ReadAsync(buffer, totalBytesRead, maxBytes - totalBytesRead, cancellationToken).ConfigureAwait(false);
#endif
                if (bytesRead == 0)
                {
                    break;
                }

                totalBytesRead += bytesRead;
            }

            if (totalBytesRead == maxBytes)
            {
                // Peek for overflow by reusing buffer[0] to avoid extra allocation.
                // Safety: if 0 bytes are returned (EOF) buffer[0] is not written and BuildResult
                // proceeds with intact content. If 1 byte is returned we throw immediately and
                // never call BuildResult, so the corrupted slot is irrelevant.
#if NET
                var extra = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
#else
                var extra = await stream.ReadAsync(buffer, 0, 1, cancellationToken).ConfigureAwait(false);
#endif
                if (extra > 0)
                {
                    ThrowSizeLimitExceeded(maxBytes);
                }
            }

            return BuildResult(buffer, totalBytesRead, contentType, fileName);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static void ValidateArguments(Stream stream, string contentType, string fileName, int maxBytes)
    {
#if NET
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(contentType);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
#else
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (contentType == null)
        {
            throw new ArgumentNullException(nameof(contentType));
        }

        if (fileName == null)
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        if (maxBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes));
        }
#endif

        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream must support reading.", nameof(stream));
        }
    }

    private static void CheckSeekableSize(Stream stream, int maxBytes)
    {
        if (!stream.CanSeek)
        {
            return;
        }

        // Clamp to zero: Position > Length is legal for seekable streams (e.g. after an explicit
        // Seek past EOF). Without the clamp, remainingBytes would be negative and the comparison
        // remainingBytes > maxBytes would silently pass even for custom Stream implementations
        // that return data beyond their declared Length.
        var remainingBytes = Math.Max(0L, stream.Length - stream.Position);
        if (remainingBytes > maxBytes)
        {
            ThrowSizeLimitExceeded(maxBytes);
        }
    }

    private static EffectiveConfigFile BuildResult(byte[] buffer, int length, string contentType, string fileName)
    {
        var content = new byte[length];
        if (length > 0)
        {
            Buffer.BlockCopy(buffer, 0, content, 0, length);
        }

        return new EffectiveConfigFile(content, contentType, fileName);
    }

    private static void ThrowSizeLimitExceeded(int maxBytes)
    {
        OpAmpClientEventSource.Log.EffectiveConfigSizeLimitExceeded(maxBytes);
        throw new InvalidDataException($"Configuration file exceeds maximum allowed size of {maxBytes} bytes.");
    }
}
