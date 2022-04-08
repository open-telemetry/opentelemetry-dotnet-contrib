using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Xunit.Sdk;

namespace OpenTelemetry.Exporter.Geneva.UnitTest
{
    public class MessagePackSerializerTests
    {
        private void AssertBytes(byte[] expected, byte[] actual, int length)
        {
            Assert.Equal(expected.Length, length);
            for (int i = 0; i < length; i++)
            {
                byte expectedByte = expected[i];
                byte actualByte = actual[i];

                Assert.True(expectedByte == actualByte,
                    String.Format($"Expected: '{(byte)expectedByte}', Actual: '{(byte)actualByte}' at offset {i}.")
                );
            }
        }

        private void MessagePackSerializer_TestSerialization(object obj)
        {
            var buffer = new byte[64 * 1024];
            var length = MessagePackSerializer.Serialize(buffer, 0, obj);
            AssertBytes(MessagePack.MessagePackSerializer.Serialize(obj), buffer, length);
        }

        private void MessagePackSerializer_TestASCIIStringSerialization(string input)
        {
            var sizeLimit = (1 << 14) - 1; // // Max length of string allowed
            var buffer = new byte[64 * 1024];
            var length = MessagePackSerializer.SerializeAsciiString(buffer, 0, input);
            var deserializedString = MessagePack.MessagePackSerializer.Deserialize<string>(buffer);
            if (!string.IsNullOrEmpty(input) && input.Length > sizeLimit)
            {
                // We truncate the string using `.` in the last three characters which takes 3 bytes of memort
                var byteCount = Encoding.ASCII.GetByteCount(input.Substring(0, sizeLimit - 3)) + 3;
                Assert.Equal(0xDA, buffer[0]);
                Assert.Equal(byteCount, (buffer[1] << 8) | buffer[2]);
                Assert.Equal(byteCount, length - 3); // First three bytes are metadata

                Assert.NotEqual(input, deserializedString);

                int i;
                for (i = 0; i < sizeLimit - 3; i++)
                {
                    Assert.Equal(input[i], deserializedString[i]);
                }

                Assert.Equal('.', deserializedString[i++]);
                Assert.Equal('.', deserializedString[i++]);
                Assert.Equal('.', deserializedString[i++]);
            }
            else
            {
                if (input != null)
                {
                    var byteCount = Encoding.ASCII.GetByteCount(input);
                    if (input.Length <= 31)
                    {
                        Assert.Equal(0xA0 | byteCount, buffer[0]);
                        Assert.Equal(byteCount, length - 1); // First one byte is metadata
                    }
                    else if (input.Length <= 255)
                    {
                        Assert.Equal(0xD9, buffer[0]);
                        Assert.Equal(byteCount, buffer[1]);
                        Assert.Equal(byteCount, length - 2); // First two bytes are metadata
                    }
                    else if (input.Length <= sizeLimit)
                    {
                        Assert.Equal(0xDA, buffer[0]);
                        Assert.Equal(byteCount, (buffer[1] << 8) | buffer[2]);
                        Assert.Equal(byteCount, length - 3); // First three bytes are metadata
                    }
                }

                Assert.Equal(input, deserializedString);
            }
        }

        private void MessagePackSerializer_TestUnicodeStringSerialization(string input)
        {
            var sizeLimit = (1 << 14) - 1; // // Max length of string allowed
            var buffer = new byte[64 * 1024];
            var length = MessagePackSerializer.SerializeUnicodeString(buffer, 0, input);

            var deserializedString = MessagePack.MessagePackSerializer.Deserialize<string>(buffer);
            if (!string.IsNullOrEmpty(input) && input.Length > sizeLimit)
            {
                // We truncate the string using `.` in the last three characters which takes 3 bytes of memory
                var byteCount = Encoding.UTF8.GetByteCount(input.Substring(0, sizeLimit - 3)) + 3;
                Assert.Equal(0xDA, buffer[0]);
                Assert.Equal(byteCount, (buffer[1] << 8) | buffer[2]);
                Assert.Equal(byteCount, length - 3); // First three bytes are metadata

                Assert.NotEqual(input, deserializedString);

                int i;
                for (i = 0; i < sizeLimit - 3; i++)
                {
                    Assert.Equal(input[i], deserializedString[i]);
                }

                Assert.Equal('.', deserializedString[i++]);
                Assert.Equal('.', deserializedString[i++]);
                Assert.Equal('.', deserializedString[i++]);
            }
            else
            {
                Assert.Equal(input, deserializedString);

                if (input != null)
                {
                    var byteCount = Encoding.UTF8.GetByteCount(input);
                    Assert.Equal(0xDA, buffer[0]);
                    Assert.Equal(byteCount, (buffer[1] << 8) | buffer[2]);
                    Assert.Equal(byteCount, length - 3); // First three bytes are metadata
                }
            }
        }

        [Fact]
        [Trait("Platform", "Any")]
        public void MessagePackSerializer_Null()
        {
            MessagePackSerializer_TestSerialization(null);
        }

        [Fact]
        [Trait("Platform", "Any")]
        public void MessagePackSerializer_Boolean()
        {
            MessagePackSerializer_TestSerialization(true);
            MessagePackSerializer_TestSerialization(false);
        }

        [Fact]
        [Trait("Platform", "Any")]
        public void MessagePackSerializer_Int()
        {
            // 8 bits
            for (sbyte value = sbyte.MinValue; value < sbyte.MaxValue; value++)
            {
                MessagePackSerializer_TestSerialization(value);
            }
            MessagePackSerializer_TestSerialization(sbyte.MaxValue);

            // 16 bits
            for (short value = short.MinValue; value < short.MaxValue; value++)
            {
                MessagePackSerializer_TestSerialization(value);
            }
            MessagePackSerializer_TestSerialization(short.MaxValue);

            // 32 bits
            MessagePackSerializer_TestSerialization(int.MinValue);
            MessagePackSerializer_TestSerialization(int.MinValue + 1);
            MessagePackSerializer_TestSerialization((int)short.MinValue - 1);
            MessagePackSerializer_TestSerialization((int)short.MinValue);
            MessagePackSerializer_TestSerialization((int)short.MinValue + 1);
            MessagePackSerializer_TestSerialization((int)sbyte.MinValue - 1);
            for (sbyte value = sbyte.MinValue; value < sbyte.MaxValue; value++)
            {
                MessagePackSerializer_TestSerialization((int)value);
            }
            MessagePackSerializer_TestSerialization((int)sbyte.MaxValue);
            MessagePackSerializer_TestSerialization((int)sbyte.MaxValue + 1);
            MessagePackSerializer_TestSerialization((int)short.MaxValue - 1);
            MessagePackSerializer_TestSerialization((int)short.MaxValue);
            MessagePackSerializer_TestSerialization((int)short.MaxValue + 1);
            MessagePackSerializer_TestSerialization(int.MaxValue - 1);
            MessagePackSerializer_TestSerialization(int.MaxValue);

            // 64 bits
            MessagePackSerializer_TestSerialization(long.MinValue);
            MessagePackSerializer_TestSerialization(long.MinValue + 1);
            MessagePackSerializer_TestSerialization((long)int.MinValue - 1);
            MessagePackSerializer_TestSerialization((long)int.MinValue);
            MessagePackSerializer_TestSerialization((long)int.MinValue + 1);
            MessagePackSerializer_TestSerialization((long)short.MinValue - 1);
            MessagePackSerializer_TestSerialization((long)short.MinValue);
            MessagePackSerializer_TestSerialization((long)short.MinValue + 1);
            MessagePackSerializer_TestSerialization((long)sbyte.MinValue - 1);
            for (sbyte value = sbyte.MinValue; value < sbyte.MaxValue; value++)
            {
                MessagePackSerializer_TestSerialization((long)value);
            }
            MessagePackSerializer_TestSerialization((long)sbyte.MaxValue);
            MessagePackSerializer_TestSerialization((long)sbyte.MaxValue + 1);
            MessagePackSerializer_TestSerialization((long)short.MaxValue - 1);
            MessagePackSerializer_TestSerialization((long)short.MaxValue);
            MessagePackSerializer_TestSerialization((long)short.MaxValue + 1);
            MessagePackSerializer_TestSerialization((long)int.MaxValue - 1);
            MessagePackSerializer_TestSerialization((long)int.MaxValue);
            MessagePackSerializer_TestSerialization((long)int.MaxValue + 1);
            MessagePackSerializer_TestSerialization(long.MaxValue - 1);
            MessagePackSerializer_TestSerialization(long.MaxValue);
        }

        [Fact]
        [Trait("Platform", "Any")]
        public void MessagePackSerializer_UInt()
        {
            // 8 bits
            for (byte value = byte.MinValue; value < byte.MaxValue; value++)
            {
                MessagePackSerializer_TestSerialization(value);
            }
            MessagePackSerializer_TestSerialization(byte.MaxValue);

            // 16 bits
            for (ushort value = ushort.MinValue; value < ushort.MaxValue; value++)
            {
                MessagePackSerializer_TestSerialization(value);
            }
            MessagePackSerializer_TestSerialization(ushort.MaxValue);

            // 32 bits
            MessagePackSerializer_TestSerialization(uint.MinValue);
            MessagePackSerializer_TestSerialization((uint)byte.MaxValue - 1);
            MessagePackSerializer_TestSerialization((uint)byte.MaxValue);
            MessagePackSerializer_TestSerialization((uint)byte.MaxValue + 1);
            MessagePackSerializer_TestSerialization((uint)ushort.MaxValue - 1);
            MessagePackSerializer_TestSerialization((uint)ushort.MaxValue);
            MessagePackSerializer_TestSerialization((uint)ushort.MaxValue + 1);
            MessagePackSerializer_TestSerialization(uint.MaxValue - 1);
            MessagePackSerializer_TestSerialization(uint.MaxValue);

            // 64 bits
            MessagePackSerializer_TestSerialization(ulong.MinValue);
            MessagePackSerializer_TestSerialization((ulong)byte.MaxValue - 1);
            MessagePackSerializer_TestSerialization((ulong)byte.MaxValue);
            MessagePackSerializer_TestSerialization((ulong)byte.MaxValue + 1);
            MessagePackSerializer_TestSerialization((ulong)ushort.MaxValue - 1);
            MessagePackSerializer_TestSerialization((ulong)ushort.MaxValue);
            MessagePackSerializer_TestSerialization((ulong)ushort.MaxValue + 1);
            MessagePackSerializer_TestSerialization((ulong)uint.MaxValue - 1);
            MessagePackSerializer_TestSerialization((ulong)uint.MaxValue);
            MessagePackSerializer_TestSerialization((ulong)uint.MaxValue + 1);
            MessagePackSerializer_TestSerialization(ulong.MaxValue - 1);
            MessagePackSerializer_TestSerialization(ulong.MaxValue);
        }

        [Fact]
        [Trait("Platform", "Any")]
        public void MessagePackSerializer_Float()
        {
            MessagePackSerializer_TestSerialization(0.0f);
            MessagePackSerializer_TestSerialization(1.0f);
            MessagePackSerializer_TestSerialization(-123.45f);
            MessagePackSerializer_TestSerialization(float.MaxValue);
            MessagePackSerializer_TestSerialization(float.MinValue);
            MessagePackSerializer_TestSerialization(float.PositiveInfinity);
            MessagePackSerializer_TestSerialization(float.NegativeInfinity);

            MessagePackSerializer_TestSerialization(0.0d);
            MessagePackSerializer_TestSerialization(3.1415926d);
            MessagePackSerializer_TestSerialization(-67.89f);
            MessagePackSerializer_TestSerialization(double.MaxValue);
            MessagePackSerializer_TestSerialization(double.MinValue);
            MessagePackSerializer_TestSerialization(double.PositiveInfinity);
            MessagePackSerializer_TestSerialization(double.NegativeInfinity);
        }

        [Fact]
        [Trait("Platform", "Any")]
        public void MessagePackSerializer_SerializeAsciiString()
        {
            MessagePackSerializer_TestASCIIStringSerialization(null);
            MessagePackSerializer_TestASCIIStringSerialization(string.Empty);
            MessagePackSerializer_TestASCIIStringSerialization("");
            MessagePackSerializer_TestASCIIStringSerialization("Hello world!");
            //fixstr stores a byte array whose length is upto 31 bytes
            MessagePackSerializer_TestASCIIStringSerialization("1234567890123456789012345678901");

            //str 8 stores a byte array whose length is upto (2^8)-1 bytes
            MessagePackSerializer_TestASCIIStringSerialization("12345678901234567890123456789012");
            MessagePackSerializer_TestASCIIStringSerialization(new string('A', byte.MaxValue));
            MessagePackSerializer_TestASCIIStringSerialization(new string('B', byte.MaxValue + 1));
            MessagePackSerializer_TestASCIIStringSerialization(new string('Z', (1 << 14) - 1));
            MessagePackSerializer_TestASCIIStringSerialization(new string('Z', 1 << 14));

            // Unicode special characters
            // SerializeAsciiString will encode non-ASCII characters with '?'
            Assert.Throws<EqualException>(() => MessagePackSerializer_TestASCIIStringSerialization("\u0418"));
        }

        [Fact]
        [Trait("Platform", "Any")]
        public void MessagePackSerializer_SerializeUnicodeString()
        {
            MessagePackSerializer_TestUnicodeStringSerialization(null);
            MessagePackSerializer_TestUnicodeStringSerialization(string.Empty);
            MessagePackSerializer_TestUnicodeStringSerialization("");
            MessagePackSerializer_TestUnicodeStringSerialization("Hello world!");
            //fixstr stores a byte array whose length is upto 31 bytes
            MessagePackSerializer_TestUnicodeStringSerialization("1234567890123456789012345678901");

            //str 8 stores a byte array whose length is upto (2^8)-1 bytes
            MessagePackSerializer_TestUnicodeStringSerialization("12345678901234567890123456789012");
            MessagePackSerializer_TestUnicodeStringSerialization(new string('A', byte.MaxValue));
            MessagePackSerializer_TestUnicodeStringSerialization(new string('B', byte.MaxValue + 1));
            MessagePackSerializer_TestUnicodeStringSerialization(new string('Z', (1 << 14) - 1));
            MessagePackSerializer_TestUnicodeStringSerialization(new string('Z', 1 << 14));

            // ill-formed UTF-8 sequence
            // This is replaced by `U+FFFD REPLACEMENT CHARACTER` in the returned string instance constructed from the byte array
            // TODO: Update this test case once the serializer starts to throw exception for ill-formed UTF-8 sequence.
            Assert.Throws<EqualException>(() => MessagePackSerializer_TestUnicodeStringSerialization("\uD801\uD802"));

            // Unicode special characters
            MessagePackSerializer_TestUnicodeStringSerialization("\u0418");
            MessagePackSerializer_TestUnicodeStringSerialization(new string('\u0418', 31));
            MessagePackSerializer_TestUnicodeStringSerialization(new string('\u0418', 50));
            MessagePackSerializer_TestUnicodeStringSerialization(new string('\u0418', (1 << 8) - 1));
            MessagePackSerializer_TestUnicodeStringSerialization(new string('\u0418', 1 << 10));
            MessagePackSerializer_TestUnicodeStringSerialization(new string('\u0418', (1 << 14) - 1));
            MessagePackSerializer_TestUnicodeStringSerialization(new string('\u0418', 1 << 14));

            // Unicode regular and special characters
            MessagePackSerializer_TestUnicodeStringSerialization("\u0418TestString");
            MessagePackSerializer_TestUnicodeStringSerialization("TestString\u0418");
            MessagePackSerializer_TestUnicodeStringSerialization("Test\u0418String");
        }

        [Fact]
        [Trait("Platform", "Any")]
        public void MessagePackSerializer_Array()
        {
            MessagePackSerializer_TestSerialization((object[])null);
            MessagePackSerializer_TestSerialization(new object[0]);

            // This object array has a custom string which will be serialized as STR16
            var objectArrayWithString = new object[] {
                "foo",
                1,
                0.6180340f,
                3.14159265358979323846264d,
            };

            var buffer = new byte[64 * 1024];
            _ = MessagePackSerializer.Serialize(buffer, 0, objectArrayWithString);
            var objectArrayWithStringDeserialized = MessagePack.MessagePackSerializer.Deserialize<object[]>(buffer);
            Assert.Equal(objectArrayWithString.Length, objectArrayWithStringDeserialized.Length);
            Assert.Equal(objectArrayWithString[0], objectArrayWithStringDeserialized[0]);
            Assert.Equal(objectArrayWithString[1], Convert.ToInt32(objectArrayWithStringDeserialized[1]));
            Assert.Equal(objectArrayWithString[2], objectArrayWithStringDeserialized[2]);
            Assert.Equal(objectArrayWithString[3], objectArrayWithStringDeserialized[3]);
        }

        [Fact]
        [Trait("Platform", "Any")]
        public void MessagePackSerializer_Map()
        {
            MessagePackSerializer_TestSerialization((Dictionary<string, object>)null);
            MessagePackSerializer_TestSerialization(new Dictionary<string, object>());

            // This dictionary has custom strings which will be serialized as STR16
            var dictionaryWithStrings = new Dictionary<string, object>
            {
                ["foo"] = 1,
                ["bar"] = "baz",
                ["golden ratio"] = 0.6180340f,
                ["pi"] = 3.14159265358979323846264d,
            };
            var buffer = new byte[64 * 1024];
            _ = MessagePackSerializer.Serialize(buffer, 0, dictionaryWithStrings);
            var dictionaryWithStringsDeserialized = MessagePack.MessagePackSerializer.Deserialize<Dictionary<string, object>>(buffer);
            Assert.Equal(dictionaryWithStrings.Count, dictionaryWithStringsDeserialized.Count);
            Assert.Equal(dictionaryWithStrings["foo"], Convert.ToInt32(dictionaryWithStringsDeserialized["foo"]));
            Assert.Equal(dictionaryWithStrings["bar"], dictionaryWithStringsDeserialized["bar"]);
            Assert.Equal(dictionaryWithStrings["golden ratio"], dictionaryWithStringsDeserialized["golden ratio"]);
            Assert.Equal(dictionaryWithStrings["pi"], dictionaryWithStringsDeserialized["pi"]);
        }
    }
}
