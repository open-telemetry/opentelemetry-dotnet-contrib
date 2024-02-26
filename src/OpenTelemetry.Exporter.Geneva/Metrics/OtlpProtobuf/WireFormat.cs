// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Geneva;

/// <summary>
/// This class contains constants and helper functions useful for dealing with
/// the Protocol Buffer wire format.
/// </summary>
internal static class WireFormat
{
    /// <summary>
    /// Wire types within protobuf encoding.
    /// </summary>
    public enum WireType : uint
    {
        /// <summary>
        /// Variable-length integer.
        /// </summary>
        Varint = 0,

        /// <summary>
        /// A fixed-length 64-bit value.
        /// </summary>
        Fixed64 = 1,

        /// <summary>
        /// A length-delimited value, i.e. a length followed by that many bytes of data.
        /// </summary>
        LengthDelimited = 2,

        /// <summary>
        /// A "start group" value.
        /// </summary>
        StartGroup = 3,

        /// <summary>
        /// An "end group" value.
        /// </summary>
        EndGroup = 4,

        /// <summary>
        /// A fixed-length 32-bit value.
        /// </summary>
        Fixed32 = 5,
    }

    private const int TagTypeBits = 3;

    /// <summary>
    /// Makes a tag value given a field number and wire type.
    /// </summary>
    /// <param name="fieldNumber">Field number specified in proto definition.</param>
    /// <param name="wireType">WireType for the field.</param>
    /// <returns>Tag value.</returns>
    internal static uint MakeTag(int fieldNumber, WireType wireType)
    {
        return (uint)(fieldNumber << TagTypeBits) | (uint)wireType;
    }
}
