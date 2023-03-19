// <copyright file="TransportSendRequest.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

// Note: StyleCop doesn't understand the C#11 "required" modifier yet. Remove
// this in the future once StyleCop is updated. See:
// https://github.com/DotNetAnalyzers/StyleCopAnalyzers/issues/3527

#pragma warning disable SA1206 // Declaration keywords should follow order

namespace OpenTelemetry.Exporter.OneCollector;

internal readonly struct TransportSendRequest
{
    public TransportSendRequest()
    {
#if !NET7_0_OR_GREATER
        // Note: This is needed because < NET7 doesn't understand required.
        this.ItemType = string.Empty;
        this.ItemStream = default!;
#endif
    }

#if NET7_0_OR_GREATER
    public required string ItemType { get; init; }

    public required OneCollectorExporterSerializationFormatType ItemSerializationFormat { get; init; }

    public required Stream ItemStream { get; init; }

    public required int NumberOfItems { get; init; }
#else
    public string ItemType { get; init; }

    public OneCollectorExporterSerializationFormatType ItemSerializationFormat { get; init; }

    public Stream ItemStream { get; init; }

    public int NumberOfItems { get; init; }
#endif
}
