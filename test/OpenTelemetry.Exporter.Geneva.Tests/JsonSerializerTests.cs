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
using Xunit;

namespace OpenTelemetry.Exporter.Geneva.UnitTest
{
    public class JsonSerializerTests
    {
        private void TestSerialization(object value, string expected)
        {
            var buffer = new byte[64 * 1024];
            var length = JsonSerializer.Serialize(buffer, 0, value);
            Assert.Equal(expected, Encoding.ASCII.GetString(buffer, 0, length));
        }

        [Fact]
        [Trait("Platform", "Any")]
        public void JsonSerializer_Null()
        {
            this.TestSerialization(null, "null");
        }

        [Fact]
        [Trait("Platform", "Any")]
        public void JsonSerializer_Boolean()
        {
            this.TestSerialization(true, "true");
            this.TestSerialization(false, "false");
        }

        [Fact]
        [Trait("Platform", "Any")]
        public void JsonSerializer_Numeric()
        {
            this.TestSerialization(0, "0");
            this.TestSerialization(123, "123");
            this.TestSerialization(-123, "-123");
            this.TestSerialization(0.0f, "0");
            this.TestSerialization(1.0f, "1");
            this.TestSerialization(3.14f, "3.14");
            this.TestSerialization(-3.14f, "-3.14");
            this.TestSerialization(0.0d, "0");
            this.TestSerialization(3.14d, "3.14");
            this.TestSerialization(3.1415926d, "3.1415926");
            this.TestSerialization(-3.1415926d, "-3.1415926");
        }

        [Fact]
        [Trait("Platform", "Any")]
        public void JsonSerializer_String()
        {
            this.TestSerialization((string)null, "null");
            this.TestSerialization(string.Empty, "''".Replace("'", "\""));
            this.TestSerialization("Hello, World!", "'Hello, World!'".Replace("'", "\""));
            this.TestSerialization("\"", "'\\\"'".Replace("'", "\""));
            this.TestSerialization("\n", "'\\n'".Replace("'", "\""));
            this.TestSerialization("\t", "'\\t'".Replace("'", "\""));
            this.TestSerialization("\0", "'\\u0000'".Replace("'", "\""));
            this.TestSerialization("\u6768", "'\\u6768'".Replace("'", "\""));
        }

        [Fact]
        [Trait("Platform", "Any")]
        public void JsonSerializer_Array()
        {
            this.TestSerialization((object[])null, "null");
            this.TestSerialization(new object[] { }, "[]");
            this.TestSerialization(new object[] { 1, 2, 3 }, "[1,2,3]");
        }

        [Fact]
        [Trait("Platform", "Any")]
        public void JsonSerializer_Map()
        {
            this.TestSerialization((Dictionary<string, object>)null, "null");
            this.TestSerialization(new Dictionary<string, object>(), "{}");
            this.TestSerialization(
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
}
