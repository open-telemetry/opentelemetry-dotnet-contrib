// <copyright file="JsonSerializerTests.cs" company="OpenTelemetry Authors">
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
using System.Text;
using OpenTelemetry.Exporter.Geneva.TldExporter;
using Xunit;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class JsonSerializerTests
{
    public static IEnumerable<object[]> Data =>
        new List<object[]>
        {
            new object[] { null, "null" },
            new object[] { true, "true" },
            new object[] { false, "false" },
            new object[] { 0, "0" },
            new object[] { 123, "123" },
            new object[] { -123, "-123" },
            new object[] { 0.0f, "0" },
            new object[] { 1.0f, "1" },
            new object[] { 3.14f, "3.14" },
            new object[] { -3.14f, "-3.14" },
            new object[] { 0.0d, "0" },
            new object[] { 3.14d, "3.14" },
            new object[] { 3.1415926d, "3.1415926" },
            new object[] { -3.1415926d, "-3.1415926" },
            new object[] { string.Empty, "''".Replace("'", "\"") },
            new object[] { "Hello, World!", "'Hello, World!'".Replace("'", "\"") },
            new object[] { "\"", "'\\\"'".Replace("'", "\"") },
            new object[] { "\n", "'\\n'".Replace("'", "\"") },
            new object[] { "\t", "'\\t'".Replace("'", "\"") },
            new object[] { "\0", "'\\u0000'".Replace("'", "\"") },
            new object[] { "\u6768", "'\\u6768'".Replace("'", "\"") },
            new object[] { Array.Empty<object>(), "[]" },
            new object[] { new object[] { 1, 2, 3 }, "[1,2,3]" },
            new object[] { new Dictionary<string, object>(), "{}" },
            new object[] { new Dictionary<string, object> { ["foo"] = 1, ["bar"] = "baz", ["golden ratio"] = 0.6180340f, ["pi"] = 3.14159265358979d }, "{'foo':1,'bar':'baz','golden ratio':0.618034,'pi':3.14159265358979}".Replace("'", "\"") },
        };

    [Theory]
    [MemberData(nameof(Data))]
    [Trait("Platform", "Any")]
    public void TestSerialization(object value, string expected)
    {
        var buffer = new byte[65_536];
        var length = JsonSerializer.Serialize(buffer, 0, value);
        Assert.Equal(expected, Encoding.ASCII.GetString(buffer, 0, length));
    }
}
