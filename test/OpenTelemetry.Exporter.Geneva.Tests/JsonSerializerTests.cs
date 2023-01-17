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

using System.Collections.Generic;
using System.Text;
using OpenTelemetry.Exporter.Geneva.TldExporter;
using Xunit;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class JsonSerializerTests
{
    private static void TestSerialization(object value, string expected)
    {
        var buffer = new byte[64 * 1024];
        var length = JsonSerializer.Serialize(buffer, 0, value);
        Assert.Equal(expected, Encoding.ASCII.GetString(buffer, 0, length));
    }

    [Fact]
    [Trait("Platform", "Any")]
    public void JsonSerializer_Null()
    {
        TestSerialization(null, "null");
    }

    [Fact]
    [Trait("Platform", "Any")]
    public void JsonSerializer_Boolean()
    {
        TestSerialization(true, "true");
        TestSerialization(false, "false");
    }

    [Fact]
    [Trait("Platform", "Any")]
    public void JsonSerializer_Numeric()
    {
        TestSerialization(0, "0");
        TestSerialization(123, "123");
        TestSerialization(-123, "-123");
        TestSerialization(0.0f, "0");
        TestSerialization(1.0f, "1");
        TestSerialization(3.14f, "3.14");
        TestSerialization(-3.14f, "-3.14");
        TestSerialization(0.0d, "0");
        TestSerialization(3.14d, "3.14");
        TestSerialization(3.1415926d, "3.1415926");
        TestSerialization(-3.1415926d, "-3.1415926");
    }

    [Fact]
    [Trait("Platform", "Any")]
    public void JsonSerializer_String()
    {
        TestSerialization((string)null, "null");
        TestSerialization(string.Empty, "''".Replace("'", "\""));
        TestSerialization("Hello, World!", "'Hello, World!'".Replace("'", "\""));
        TestSerialization("\"", "'\\\"'".Replace("'", "\""));
        TestSerialization("\n", "'\\n'".Replace("'", "\""));
        TestSerialization("\t", "'\\t'".Replace("'", "\""));
        TestSerialization("\0", "'\\u0000'".Replace("'", "\""));
        TestSerialization("\u6768", "'\\u6768'".Replace("'", "\""));
    }

    [Fact]
    [Trait("Platform", "Any")]
    public void JsonSerializer_Array()
    {
        TestSerialization((object[])null, "null");
        TestSerialization(new object[] { }, "[]");
        TestSerialization(new object[] { 1, 2, 3 }, "[1,2,3]");
    }

    [Fact]
    [Trait("Platform", "Any")]
    public void JsonSerializer_Map()
    {
        TestSerialization((Dictionary<string, object>)null, "null");
        TestSerialization(new Dictionary<string, object>(), "{}");
        TestSerialization(
            new Dictionary<string, object>
            {
                ["foo"] = 1,
                ["bar"] = "baz",
                ["golden ratio"] = 0.6180340f,
                ["pi"] = 3.14159265358979d,
            },
            "{'foo':1,'bar':'baz','golden ratio':0.618034,'pi':3.14159265358979}".Replace("'", "\""));
    }
}
