// <copyright file="IGenevaTableColumnNameResolver.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;

namespace OpenTelemetry.Exporter.Geneva;

/// <summary>
/// Describes a service used to resolve the column names for a Geneva table.
/// </summary>
public interface IGenevaTableColumnNameResolver
{
    /// <summary>
    /// Resolve column names for a given table name.
    /// </summary>
    /// <param name="tableName">Table name.</param>
    /// <returns>The set of column names for the given table or <see
    /// langword="null"/> to fallback to the default behavior.</returns>
    ISet<string> ResolveColumnNamesForTableName(in GenevaTableName tableName);
}
