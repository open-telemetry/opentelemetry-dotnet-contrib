// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// Note: StyleCop doesn't understand the C#11 "required" modifier yet. Remove
// this in the future once StyleCop is updated. See:
// https://github.com/DotNetAnalyzers/StyleCopAnalyzers/issues/3527

#pragma warning disable SA1206 // Declaration keywords should follow order

namespace OpenTelemetry.Exporter.OneCollector;

internal readonly struct BatchSerializationResult
{
#if NET7_0_OR_GREATER
    public required int NumberOfItemsSerialized { get; init; }

    public required long PayloadSizeInBytes { get; init; }
#else
    public int NumberOfItemsSerialized { get; init; }

    public long PayloadSizeInBytes { get; init; }
#endif

    public long? PayloadOverflowItemSizeInBytes { get; init; }
}
