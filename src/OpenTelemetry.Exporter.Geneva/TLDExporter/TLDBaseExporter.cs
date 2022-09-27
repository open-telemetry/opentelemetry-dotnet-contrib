// <copyright file="TLDBaseExporter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Geneva.TLDExporter;

internal abstract class TLDBaseExporter<T> : GenevaBaseExporter<T>
    where T : class
{
    internal static readonly IReadOnlyDictionary<string, string> V40_PART_A_TLD_MAPPING = new Dictionary<string, string>
    {
        // Part A
        [Schema.V40.PartA.IKey] = "iKey",
        [Schema.V40.PartA.Name] = "name",
        [Schema.V40.PartA.Time] = "time",

        // Part A Application Extension
        [Schema.V40.PartA.Extensions.App.Id] = "ext_app_id",
        [Schema.V40.PartA.Extensions.App.Ver] = "ext_app_ver",

        // Part A Cloud Extension
        [Schema.V40.PartA.Extensions.Cloud.Role] = "ext_cloud_role",
        [Schema.V40.PartA.Extensions.Cloud.RoleInstance] = "ext_cloud_roleInstance",
        [Schema.V40.PartA.Extensions.Cloud.RoleVer] = "ext_cloud_roleVer",

        // Part A Os extension
        [Schema.V40.PartA.Extensions.Os.Name] = "ext_os_name",
        [Schema.V40.PartA.Extensions.Os.Ver] = "ext_os_ver",
    };
}
