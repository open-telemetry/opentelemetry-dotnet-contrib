// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.AWS;

/// <summary>
/// Generate AWS X-Ray compatible trace IDs.
/// See https://docs.aws.amazon.com/xray/latest/devguide/xray-api-sendingdata.html#xray-api-traceids.
/// </summary>
public static class AWSXRayIdGenerator
{
#if !NET
    // RandomNumberGenerator.Fill() is not available on .NET Framework or .NET Standard 2.0.
    // The instances returned by RandomNumberGenerator.Create() are thread-safe, so a single
    // shared instance is used rather than creating one per trace ID.
    private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();
#endif

    /// <summary>
    /// Sets <see cref="Activity.TraceIdGenerator"/> to <see cref="GenerateAWSXRayCompatibleTraceId"/>.
    /// </summary>
    public static void ReplaceTraceId()
        => Activity.TraceIdGenerator = GenerateAWSXRayCompatibleTraceId;

    /// <summary>
    /// Sets <see cref="Activity.TraceIdGenerator"/> to <see cref="GenerateAWSXRayCompatibleTraceId"/>.
    /// </summary>
    /// <param name="sampler">Unused. (See deprecation message.)</param>
    [Obsolete($"The X-Ray ID generator does not need to update the sampling decision. Use {nameof(ReplaceTraceId)}() instead.")]
    public static void ReplaceTraceId(Sampler sampler)
        => ReplaceTraceId();

    /// <summary>
    /// Generates an AWS X-Ray compatible trace ID.
    /// </summary>
    /// <returns>
    /// An <see cref="ActivityTraceId"/> whose first 4 bytes are the big-endian unix timestamp (in seconds) and whose
    /// remaining 12 bytes are randomly generated.
    /// </returns>
    internal static ActivityTraceId GenerateAWSXRayCompatibleTraceId()
    {
        // intentionally truncating to 4 bytes because AWS X-Ray requires 8 hex characters
        var seconds = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

#if NET
        Span<byte> buffer = stackalloc byte[16];

        // fill the rest of the buffer with random bytes
        RandomNumberGenerator.Fill(buffer.Slice(4, 12));
#else
        var buffer = new byte[16];

        // fill the whole buffer with random bytes; the first 4 are overwritten by the timestamp below
        Rng.GetBytes(buffer);
#endif

        BinaryPrimitives.WriteUInt32BigEndian(buffer, seconds);

        return ActivityTraceId.CreateFromBytes(buffer);
    }
}
