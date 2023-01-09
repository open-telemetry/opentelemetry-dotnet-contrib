// <copyright file="GenevaTableName.cs" company="OpenTelemetry Authors">
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

using System;
using System.Text;

namespace OpenTelemetry.Exporter.Geneva;

/// <summary>
/// Stores details about a Geneva table name.
/// </summary>
public readonly ref struct GenevaTableName
{
    private readonly ReadOnlySpan<byte> tableNameStr8;

    internal GenevaTableName(ReadOnlySpan<byte> tableNameStr8)
    {
        this.tableNameStr8 = tableNameStr8;
    }

    /// <summary>
    /// Gets the ascii bytes of the Geneva table name.
    /// </summary>
    /// <returns>The table name.</returns>
    public ReadOnlySpan<byte> ToAsciiBytes() => this.tableNameStr8.Slice(2);

    internal ReadOnlySpan<byte> ToAsciiStr8() => this.tableNameStr8;

    /// <inheritdoc/>
    public override string ToString()
    {
#if NET6_0_OR_GREATER
        return Encoding.ASCII.GetString(this.tableNameStr8.Slice(2));
#else
        return Encoding.ASCII.GetString(this.tableNameStr8.Slice(2).ToArray());
#endif
    }
}
