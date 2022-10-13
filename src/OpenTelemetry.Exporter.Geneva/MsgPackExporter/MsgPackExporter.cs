// <copyright file="MsgPackExporter.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;

namespace OpenTelemetry.Exporter.Geneva;

internal abstract class MsgPackExporter
{
    internal static readonly IReadOnlyDictionary<string, string> V40_PART_A_MAPPING = new Dictionary<string, string>
    {
        // Part A
        [Schema.V40.PartA.IKey] = "env_iKey",
        [Schema.V40.PartA.Name] = "env_name",
        [Schema.V40.PartA.Ver] = "env_ver",
        [Schema.V40.PartA.Time] = "env_time",

        // Part A Application Extension
        [Schema.V40.PartA.Extensions.App.Id] = "env_app_id",
        [Schema.V40.PartA.Extensions.App.Ver] = "env_app_ver",

        // Part A Cloud Extension
        [Schema.V40.PartA.Extensions.Cloud.Role] = "env_cloud_role",
        [Schema.V40.PartA.Extensions.Cloud.RoleInstance] = "env_cloud_roleInstance",
        [Schema.V40.PartA.Extensions.Cloud.RoleVer] = "env_cloud_roleVer",

        // Part A Os extension
        [Schema.V40.PartA.Extensions.Os.Name] = "env_os_name",
        [Schema.V40.PartA.Extensions.Os.Ver] = "env_os_ver",
    };

    protected static int AddPartAField(byte[] buffer, int cursor, string name, object value)
    {
        if (V40_PART_A_MAPPING.TryGetValue(name, out string replacementKey))
        {
            cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, replacementKey);
        }
        else
        {
            cursor = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, name);
        }

        cursor = MessagePackSerializer.Serialize(buffer, cursor, value);
        return cursor;
    }

    protected static int AddPartAField(byte[] buffer, int cursor, string name, Span<byte> value)
    {
        if (V40_PART_A_MAPPING.TryGetValue(name, out string replacementKey))
        {
            cursor = MessagePackSerializer.SerializeAsciiString(buffer, cursor, replacementKey);
        }
        else
        {
            cursor = MessagePackSerializer.SerializeUnicodeString(buffer, cursor, name);
        }

        cursor = MessagePackSerializer.SerializeSpan(buffer, cursor, value);
        return cursor;
    }
}
