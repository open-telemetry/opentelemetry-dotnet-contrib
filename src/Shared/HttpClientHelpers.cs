// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Buffers;
#endif
using System.Text;
using OpenTelemetry.Internal;

namespace System.Net.Http;

internal static class HttpClientHelpers
{
    private const int DefaultMessageSizeLimit = 4 * 1024 * 1024; // 4MiB

    internal static async Task<byte[]> GetResponseBodyAsByteArrayAsync(
        int limit,
        HttpResponseMessage? httpResponse,
        CancellationToken cancellationToken)
    {
        Guard.ThrowIfOutOfRange(limit, nameof(limit), 1, int.MaxValue);

        if (httpResponse?.Content is null)
        {
            return [];
        }

        // Check Content-Length before reading if the header is present.
        if (httpResponse.Content.Headers.ContentLength > limit)
        {
            throw new InvalidOperationException($"Response body exceeded the size limit of {limit} bytes.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var stream = await httpResponse.Content
#if NET
            .ReadAsStreamAsync(cancellationToken)
#else
            .ReadAsStreamAsync()
#endif
            .ConfigureAwait(false);

#if NET
        await using (stream.ConfigureAwait(false))
#else
        using (stream)
#endif
        {
            var length = GetBufferLength(stream, limit);

#if NET
            var buffer = ArrayPool<byte>.Shared.Rent(length);
#else
            var buffer = new byte[length];
#endif

            try
            {
                var totalRead = 0;

                // Read raw bytes so the size limit applies to bytes rather than characters
                while (totalRead < limit && !cancellationToken.IsCancellationRequested)
                {
                    var bytesRead = await stream
#if NET || NETSTANDARD2_1_OR_GREATER
                        .ReadAsync(buffer.AsMemory(totalRead, limit - totalRead), cancellationToken)
#else
                        .ReadAsync(buffer, totalRead, limit - totalRead, cancellationToken)
#endif
                        .ConfigureAwait(false);

                    if (bytesRead is 0)
                    {
                        break;
                    }

                    totalRead += bytesRead;
                }

                // We've read exactly limit bytes. Check if there's more data.
                var probe = new byte[1];

                var extra = await stream
#if NET || NETSTANDARD2_1_OR_GREATER
                    .ReadAsync(probe.AsMemory(0, 1), cancellationToken)
#else
                    .ReadAsync(probe, 0, 1, cancellationToken)
#endif
                    .ConfigureAwait(false);

                if (extra > 0)
                {
                    // + 1: we read exactly MaxMessageSize bytes and confirmed at least one more byte exists.
                    throw new InvalidOperationException($"Response body exceeded the size limit of {limit} bytes.");
                }

                cancellationToken.ThrowIfCancellationRequested();

                var result = new byte[totalRead];

                Buffer.BlockCopy(buffer, 0, result, 0, totalRead);

                return result;
            }
            finally
            {
#if NET
                // Clear the rented buffer to avoid leaking sensitive data, then return it to the pool.
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
#endif
            }
        }
    }

    internal static string? TryGetResponseBodyAsString(HttpResponseMessage? httpResponse, CancellationToken cancellationToken)
        => GetResponseBodyAsString(allowTruncation: true, DefaultMessageSizeLimit, httpResponse, cancellationToken);

    internal static string? GetResponseBodyAsString(HttpResponseMessage? httpResponse, CancellationToken cancellationToken)
        => GetResponseBodyAsString(httpResponse, DefaultMessageSizeLimit, cancellationToken);

    internal static string? GetResponseBodyAsString(HttpResponseMessage? httpResponse, int limit, CancellationToken cancellationToken)
        => GetResponseBodyAsString(allowTruncation: false, limit, httpResponse, cancellationToken);

    private static string? GetResponseBodyAsString(
        bool allowTruncation,
        int limit,
        HttpResponseMessage? httpResponse,
        CancellationToken cancellationToken)
    {
        Guard.ThrowIfOutOfRange(limit, nameof(limit), 1, int.MaxValue);

        if (httpResponse?.Content is null)
        {
            return null;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            if (allowTruncation)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        try
        {
#if NET
            using var stream = httpResponse.Content.ReadAsStream(cancellationToken);
#else
            using var stream = httpResponse.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
#endif

            var length = GetBufferLength(stream, limit);

#if NET
            var buffer = ArrayPool<byte>.Shared.Rent(length);
#else
            var buffer = new byte[length];
#endif

            try
            {
                var totalRead = 0;

                // Read raw bytes so the size limit applies to bytes rather than characters
                while (totalRead < limit && !cancellationToken.IsCancellationRequested)
                {
                    var bytesRead = stream.Read(buffer, totalRead, length - totalRead);

                    if (bytesRead is 0)
                    {
                        break;
                    }

                    totalRead += bytesRead;
                }

                // We've read exactly limit bytes. Check if there's more data.
                var probe = new byte[1];

#if NETFRAMEWORK || NETSTANDARD
                var extra = stream.Read(probe, 0, 1);
#else
                var extra = stream.Read(probe);
#endif

                if (extra > 0 && !allowTruncation)
                {
                    // + 1: we read exactly MaxMessageSize bytes and confirmed at least one more byte exists.
                    throw new InvalidOperationException($"Response body exceeded the size limit of {limit} bytes.");
                }

                if (!allowTruncation)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                // Decode using the charset from the response content headers, if available
                var encoding = GetEncoding(httpResponse.Content.Headers.ContentType?.CharSet);
                var result = encoding.GetString(buffer, 0, totalRead);

                if (extra > 0)
                {
                    result += "[TRUNCATED]";
                }

                return result;
            }
            finally
            {
#if NET
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
#endif
            }
        }
        catch (Exception) when (allowTruncation)
        {
            return null;
        }
    }

    private static int GetBufferLength(Stream stream, int limit)
    {
        try
        {
            // Avoid allocating an overly large buffer if the stream is smaller than the size limit
            return stream.Length < limit ? (int)stream.Length : limit;
        }
        catch (Exception)
        {
            // Not all Stream types support Length, so default to the maximum
            return limit;
        }
    }

    private static Encoding GetEncoding(string? name)
    {
        Encoding encoding = Encoding.UTF8;

        if (!string.IsNullOrWhiteSpace(name))
        {
            try
            {
                encoding = Encoding.GetEncoding(name);
            }
            catch (Exception)
            {
                // Invalid encoding name
            }
        }

        return encoding;
    }
}
