// <copyright file="AssertUtils.cs" company="OpenTelemetry Authors">
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

using Xunit;

namespace OpenTelemetry.Exporter.InfluxDB.Tests.Utils;

public static class AssertUtils
{
    public static void HasField<TKey, TValue>(TKey expectedKey, TValue expectedValue, IReadOnlyDictionary<TKey, object> collection)
        where TKey : notnull
    {
        Assert.Contains(expectedKey, collection);
        Assert.Equal(expectedValue, collection[expectedKey]);
    }

    public static void HasTag(string expectedKey, string expectedValue, int index, IReadOnlyList<KeyValuePair<string, string>> collection)
    {
        Assert.Equal(expectedKey, collection[index].Key);
        Assert.Equal(expectedValue, collection[index].Value);
    }
}
