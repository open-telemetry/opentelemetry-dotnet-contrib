// <copyright file="PointDataExtensions.cs" company="OpenTelemetry Authors">
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
using InfluxDB.Client.Writes;

namespace OpenTelemetry.Exporter.InfluxDB;

internal static class PointDataExtensions
{
    public static PointData Tags(this PointData pointData, ReadOnlyTagCollection tags)
    {
        foreach (var tag in tags)
        {
            pointData = pointData.Tag(tag.Key, tag.Value.ToString());
        }

        return pointData;
    }

    public static PointData Tags(this PointData pointData, IEnumerable<KeyValuePair<string, object>>? tags)
    {
        if (tags == null)
        {
            return pointData;
        }

        foreach (var tag in tags)
        {
            pointData = pointData.Tag(tag.Key, tag.Value.ToString());
        }

        return pointData;
    }
}
