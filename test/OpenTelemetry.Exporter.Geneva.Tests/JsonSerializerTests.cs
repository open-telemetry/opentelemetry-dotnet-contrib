// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable disable

using System.Globalization;
using System.Text;
using OpenTelemetry.Exporter.Geneva.Tld;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public static class JsonSerializerTests
{
    public static IEnumerable<object[]> Data =>
        [
            [null, "null"],
            [true, "true"],
            [false, "false"],
            [0, "0"],
            [123, "123"],
            [-123, "-123"],
            [0.0f, "0"],
            [1.0f, "1"],
            [3.14f, "3.14"],
            [-3.14f, "-3.14"],
            [0.0d, "0"],
            [3.14d, "3.14"],
            [3.1415926d, "3.1415926"],
            [-3.1415926d, "-3.1415926"],
            [string.Empty, "''".Replace("'", "\"")],
            ["Hello, World!", "'Hello, World!'".Replace("'", "\"")],
            ["\"", "'\\\"'".Replace("'", "\"")],
            ["\n", "'\\n'".Replace("'", "\"")],
            ["\t", "'\\t'".Replace("'", "\"")],
            ["\0", "'\\u0000'".Replace("'", "\"")],
            ["\u6768", "'\\u6768'".Replace("'", "\"")],
            [Array.Empty<object>(), "[]"],
            [new object[] { 1, 2, 3 }, "[1,2,3]"],
            [new Dictionary<string, object>(), "{}"],
            [new Dictionary<string, object> { ["foo"] = 1, ["bar"] = "baz", ["golden ratio"] = 0.6180340f, ["pi"] = 3.14159265358979d }, "{'foo':1,'bar':'baz','golden ratio':0.618034,'pi':3.14159265358979}".Replace("'", "\"")],
        ];

    public static IEnumerable<object[]> NumericData =>
        [
            [byte.MinValue],
            [byte.MaxValue],
            [sbyte.MinValue],
            [sbyte.MaxValue],
            [short.MinValue],
            [short.MaxValue],
            [ushort.MinValue],
            [ushort.MaxValue],
            [int.MinValue],
            [int.MaxValue],
            [uint.MinValue],
            [uint.MaxValue],
            [long.MinValue],
            [long.MaxValue],
            [ulong.MinValue],
            [ulong.MaxValue],
            [float.MinValue],
            [float.MaxValue],
            [float.Epsilon],
            [-0.0f],
            [float.NaN],
            [float.PositiveInfinity],
            [float.NegativeInfinity],
            [1.234567e-20f],
            [double.MinValue],
            [double.MaxValue],
            [double.Epsilon],
            [-0.0d],
            [double.NaN],
            [double.PositiveInfinity],
            [double.NegativeInfinity],
            [1.2345678901234567e-100d],
        ];

    [Theory]
    [MemberData(nameof(NumericData))]
    [Trait("Platform", "Any")]
    public static void NumericSerializationMatchesInvariantFormatting(object value)
    {
        const int offset = 7;
        var expected = Convert.ToString(value, CultureInfo.InvariantCulture);
        var buffer = new byte[offset + expected.Length + 4];

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var cursor = JsonSerializer.Serialize(buffer, offset, value);

            Assert.Equal(offset + expected.Length, cursor);
            Assert.Equal(expected, Encoding.ASCII.GetString(buffer, offset, cursor - offset));
            Assert.All(buffer.Take(offset), b => Assert.Equal(0, b));
            Assert.All(buffer.Skip(cursor), b => Assert.Equal(0, b));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [MemberData(nameof(NumericData))]
    [Trait("Platform", "Any")]
    public static void NumericSerializationFitsExactBuffer(object value)
    {
        var expected = Convert.ToString(value, CultureInfo.InvariantCulture);
        var buffer = new byte[expected.Length];

        Assert.Equal(buffer.Length, JsonSerializer.Serialize(buffer, 0, value));
        Assert.Equal(expected, Encoding.ASCII.GetString(buffer));
    }

    [Theory]
    [MemberData(nameof(NumericData))]
    [Trait("Platform", "Any")]
    public static void NumericSerializationReportsBufferOverflow(object value)
    {
        var expected = Convert.ToString(value, CultureInfo.InvariantCulture);
        var buffer = new byte[expected.Length - 1];

        var exception = Record.Exception(() => JsonSerializer.Serialize(buffer, 0, value));

        Assert.NotNull(exception);
        Assert.True(GenevaBufferOverflowExceptionHelper.IsLogBufferOverflow(exception));
    }

    [Theory]
    [MemberData(nameof(Data))]
    [Trait("Platform", "Any")]
    public static void TestSerialization(object value, string expected)
    {
        var buffer = new byte[65_536];
        var length = JsonSerializer.Serialize(buffer, 0, value);
        Assert.Equal(expected, Encoding.ASCII.GetString(buffer, 0, length));
    }

    [Fact]
    [Trait("Platform", "Any")]
    public static void JsonSerializer_Null()
    {
        TestSerialization(null, "null");
    }

    [Fact]
    [Trait("Platform", "Any")]
    public static void JsonSerializer_Boolean()
    {
        TestSerialization(true, "true");
        TestSerialization(false, "false");
    }

    [Fact]
    [Trait("Platform", "Any")]
    public static void JsonSerializer_Numeric()
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
    public static void JsonSerializer_String()
    {
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
    public static void JsonSerializer_Array()
    {
        TestSerialization(Array.Empty<object>(), "[]");
        TestSerialization(new object[] { 1, 2, 3 }, "[1,2,3]");
    }

    [Fact]
    [Trait("Platform", "Any")]
    public static void JsonSerializer_Map()
    {
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
