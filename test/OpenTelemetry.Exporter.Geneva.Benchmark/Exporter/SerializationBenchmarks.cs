// <copyright file="SerializationBenchmarks.cs" company="OpenTelemetry Authors">
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
using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.TraceLoggingDynamic;

/*
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=7.0.100-preview.6.22352.1
  [Host]     : .NET 6.0.8 (6.0.822.36306), X64 RyuJIT
  DefaultJob : .NET 6.0.8 (6.0.822.36306), X64 RyuJIT


|                         Method |     Mean |    Error |   StdDev | Allocated |
|------------------------------- |---------:|---------:|---------:|----------:|
|             TLD_SerializeUInt8 | 30.41 ns | 0.136 ns | 0.127 ns |         - |
|         MsgPack_SerializeUInt8 | 10.82 ns | 0.057 ns | 0.048 ns |         - |
|       TLD_SerializeAsciiString | 35.99 ns | 0.281 ns | 0.235 ns |         - |
|   MsgPack_SerializeAsciiString | 19.62 ns | 0.407 ns | 0.418 ns |         - |
|  TLD_SerializeUnicodeSubString | 46.31 ns | 0.405 ns | 0.379 ns |         - |
|     TLD_SerializeUnicodeString | 46.70 ns | 0.343 ns | 0.320 ns |         - |
| MsgPack_SerializeUnicodeString | 24.36 ns | 0.172 ns | 0.144 ns |         - |
|          TLD_SerializeDateTime | 54.17 ns | 0.492 ns | 0.436 ns |         - |
|      MsgPack_SerializeDateTime | 39.84 ns | 0.592 ns | 0.495 ns |         - |
|                      TLD_Reset | 16.82 ns | 0.155 ns | 0.145 ns |         - |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmark.Exporter
{
    [MemoryDiagnoser]
    public class SerializationBenchmarks
    {
        private const int StringLengthLimit = (1 << 14) - 1;
        private readonly EventBuilder eventBuilder = new(Encoding.ASCII);
        private readonly byte[] buffer = new byte[65360];

        [Benchmark]
        public void TLD_SerializeUInt8()
        {
            this.eventBuilder.Reset("test");
            this.eventBuilder.AddUInt8("Number", 123);
        }

        [Benchmark]
        public void MsgPack_SerializeUInt8()
        {
            var cursor = MessagePackSerializer.SerializeAsciiString(this.buffer, 0, "Number");
            MessagePackSerializer.SerializeUInt8(this.buffer, cursor, 123);
        }

        [Benchmark]
        public void TLD_SerializeAsciiString()
        {
            this.eventBuilder.Reset("test");
            this.eventBuilder.AddCountedString("name", "Span");
        }

        [Benchmark]
        public void MsgPack_SerializeAsciiString()
        {
            var cursor = MessagePackSerializer.SerializeAsciiString(this.buffer, 0, "name");
            MessagePackSerializer.SerializeAsciiString(this.buffer, cursor, "Span");
        }

        [Benchmark]
        public void TLD_SerializeUnicodeSubString()
        {
            this.eventBuilder.Reset("test");
            this.eventBuilder.AddCountedAnsiString("name", "Span", Encoding.UTF8, 0, Math.Min("Span".Length, StringLengthLimit));
        }

        [Benchmark]
        public void TLD_SerializeUnicodeString()
        {
            this.eventBuilder.Reset("test");
            this.eventBuilder.AddCountedAnsiString("name", "Span", Encoding.UTF8);
        }

        [Benchmark]
        public void MsgPack_SerializeUnicodeString()
        {
            var cursor = MessagePackSerializer.SerializeAsciiString(this.buffer, 0, "name");
            MessagePackSerializer.SerializeUnicodeString(this.buffer, cursor, "Span");
        }

        [Benchmark]
        public void TLD_SerializeDateTime()
        {
            this.eventBuilder.Reset("test");
            this.eventBuilder.AddFileTime("time", DateTime.UtcNow);
        }

        [Benchmark]
        public void MsgPack_SerializeDateTime()
        {
            var cursor = MessagePackSerializer.SerializeAsciiString(this.buffer, 0, "time");
            MessagePackSerializer.SerializeUtcDateTime(this.buffer, cursor, DateTime.UtcNow);
        }

        [Benchmark]
        public void TLD_Reset()
        {
            this.eventBuilder.Reset("test");
        }
    }
}
