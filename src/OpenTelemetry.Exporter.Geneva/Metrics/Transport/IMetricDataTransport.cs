// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Geneva;

internal interface IMetricDataTransport : IDisposable
{
    /// <summary>
    /// Writes a standard metric event containing only a single value.
    /// </summary>
    /// <param name="eventType">Type of the event.</param>
    /// <param name="body">The byte array containing the serialized data.</param>
    /// <param name="size">Length of the payload (fixed + variable).</param>
    void Send(
        MetricEventType eventType,
        byte[] body,
        int size);

    /// <summary>
    /// Writes a standard metric event containing only a single value.
    /// </summary>
    /// <param name="body">The byte array containing the serialized data.</param>
    /// <param name="size">Length of the payload (fixed + variable).</param>
    void SendOtlpProtobufEvent(
        byte[] body,
        int size);

    /// <summary>
    /// Attempts to append an OTLP protobuf event to an internal accumulation
    /// buffer to be flushed later as a single transport write. The bytes
    /// referenced by <paramref name="body"/> must be copied by the
    /// implementation because the caller may reuse the buffer immediately.
    /// </summary>
    /// <param name="body">The byte array containing the serialized event.</param>
    /// <param name="size">Length of the serialized event in bytes.</param>
    /// <returns>
    /// <c>true</c> if the event was appended (or sent immediately by
    /// transports that do not batch). <c>false</c> if the accumulation buffer
    /// would overflow; the caller is expected to call
    /// <see cref="FlushOtlpProtobufEvents"/> and retry.
    /// </returns>
    bool TryAppendOtlpProtobufEvent(
        byte[] body,
        int size);

    /// <summary>
    /// Flushes any events previously buffered by
    /// <see cref="TryAppendOtlpProtobufEvent"/> as a single transport write.
    /// Implementations that do not batch should treat this as a no-op.
    /// On both success and failure the internal accumulation must be reset
    /// so that no bytes leak into a subsequent export.
    /// </summary>
    void FlushOtlpProtobufEvents();
}
