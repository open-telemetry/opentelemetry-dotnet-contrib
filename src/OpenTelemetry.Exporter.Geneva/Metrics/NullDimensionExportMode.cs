// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Geneva;

/// <summary>
/// Defines modes for exporting metric dimensions whose value is <see langword="null"/>.
/// Currently applicable only to the Metrics signal when OTLP protobuf encoding is used.
/// </summary>
public enum NullDimensionExportMode
{
    /// <summary>
    /// Dimensions with a <see langword="null"/> value are dropped and not exported.
    /// </summary>
    /// <remarks>
    /// This is the default. Because the dimension name is omitted entirely, any
    /// Geneva Metrics (MDM) pre-aggregate which includes that dimension will not
    /// match the emitted time series.
    /// </remarks>
    Drop,

    /// <summary>
    /// Dimensions with a <see langword="null"/> value are exported with an empty string value.
    /// </summary>
    /// <remarks>
    /// This matches the behavior of the TLV encoding, which converts a <see langword="null"/>
    /// dimension value to an empty string, and causes Geneva Metrics (MDM) to record the
    /// dimension as <c>__Empty</c>. Use this mode to keep pre-aggregates which include
    /// optional dimensions working when moving from the TLV encoding to OTLP protobuf encoding.
    /// </remarks>
    ExportAsEmptyString,
}
