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

    internal static string? TryGetResponseBodyAsString(HttpResponseMessage httpResponse, CancellationToken cancellationToken)
        => TryGetResponseBodyAsString(DefaultMessageSizeLimit, httpResponse, cancellationToken);

    internal static string? TryGetResponseBodyAsString(
        int limit,
        HttpResponseMessage httpResponse,
        CancellationToken cancellationToken)
    {
        Guard.ThrowIfNull(httpResponse, nameof(httpResponse));
        Guard.ThrowIfOutOfRange(limit, nameof(limit), 1, int.MaxValue);

        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        try
        {
#if NET
            var stream = httpResponse.Content.ReadAsStream(cancellationToken);
#else
            var stream = httpResponse.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
#endif

            var length = GetBufferLength(stream, limit);

#if NET
            var buffer = ArrayPool<byte>.Shared.Rent(length);
#else
            var buffer = new byte[length];
#endif

            string result;

            try
            {
                var count = 0;

                // Read raw bytes so the size limit applies to bytes rather than characters
                while (count < length && !cancellationToken.IsCancellationRequested)
                {
                    var read = stream.Read(buffer, count, length - count);

                    if (read is 0)
                    {
                        break;
                    }

                    count += read;
                }

                // Decode using the charset from the response content headers, if available
                var encoding = GetEncoding(httpResponse.Content.Headers.ContentType?.CharSet);
                result = encoding.GetString(buffer, 0, count);

                if (result.Length == limit)
                {
                    result += "[TRUNCATED]";
                }
            }
            finally
            {
#if NET
                ArrayPool<byte>.Shared.Return(buffer);
#endif
            }

            return result;
        }
        catch (Exception)
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
