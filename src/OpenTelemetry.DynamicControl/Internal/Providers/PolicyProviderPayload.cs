// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;

namespace OpenTelemetry.DynamicControl.Internal.Providers;

/// <summary>
/// A complete raw policy payload received from a single provider.
/// </summary>
/// <remarks>
/// <para>
/// Content is copied without decoding, preserving the transport's bytes. The parser
/// determines whether invalid content rejects an entry or the entire payload.
/// </para>
/// <para>
/// Empty content is legal at this layer. The parser determines what an empty payload means.
/// </para>
/// </remarks>
internal sealed class PolicyProviderPayload : IDisposable
{
    private byte[]? rentedBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyProviderPayload"/> class.
    /// </summary>
    /// <param name="content">
    /// The UTF-8-encoded payload bytes. The bytes are copied; callers must not modify
    /// them during construction and may reuse or release their buffer afterwards.
    /// </param>
    /// <param name="version">
    /// The change-detection token for this payload. Defaults to
    /// <see cref="PolicyProviderVersion.Empty"/>, which disables suppression so every
    /// submission is applied.
    /// </param>
    public PolicyProviderPayload(
        ReadOnlySpan<byte> content,
        PolicyProviderVersion version = default)
    {
        this.Version = version;

        if (content.IsEmpty)
        {
            this.Content = ReadOnlyMemory<byte>.Empty;
            return;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(content.Length);
        content.CopyTo(buffer);
        this.rentedBuffer = buffer;
        this.Content = new ReadOnlyMemory<byte>(buffer, 0, content.Length);
    }

    /// <summary>
    /// Gets the payload's owned, read-only copy of the UTF-8-encoded bytes.
    /// </summary>
    /// <remarks>
    /// Backed by a pooled array. Invalid once <see cref="Dispose"/> has been called.
    /// </remarks>
    public ReadOnlyMemory<byte> Content { get; private set; }

    /// <summary>
    /// Gets the change-detection token for this payload.
    /// </summary>
    /// <remarks>
    /// When <see cref="PolicyProviderVersion.IsEmpty"/> is <see langword="true"/>, the
    /// store applies every submission regardless of content; no change detection is performed.
    /// </remarks>
    public PolicyProviderVersion Version { get; }

    /// <summary>
    /// Returns the pooled buffer backing <see cref="Content"/>, if any, to
    /// <see cref="ArrayPool{T}.Shared"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Content"/> must not be read after this call.
    /// </remarks>
    public void Dispose()
    {
        var buffer = this.rentedBuffer;
        this.rentedBuffer = null;

        if (buffer is not null)
        {
            this.Content = ReadOnlyMemory<byte>.Empty;
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
