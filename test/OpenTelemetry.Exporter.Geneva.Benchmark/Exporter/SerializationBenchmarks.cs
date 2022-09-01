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


|                    Method |     Mean |    Error |   StdDev | Allocated |
|-------------------------- |---------:|---------:|---------:|----------:|
|        TLD_SerializeUInt8 | 31.09 ns | 0.203 ns | 0.190 ns |         - |
|    MsgPack_SerializeUInt8 | 10.87 ns | 0.042 ns | 0.039 ns |         - |
|       TLD_SerializeString | 36.81 ns | 0.168 ns | 0.131 ns |         - |
|   MsgPack_SerializeString | 19.69 ns | 0.054 ns | 0.048 ns |         - |
|     TLD_SerializeDateTime | 54.22 ns | 0.378 ns | 0.353 ns |         - |
| MsgPack_SerializeDateTime | 36.95 ns | 0.172 ns | 0.161 ns |         - |
|                 TLD_Reset | 16.43 ns | 0.045 ns | 0.040 ns |         - |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmark.Exporter
{
    [MemoryDiagnoser]
    public class SerializationBenchmarks
    {
        private readonly EventBuilder eventBuilder = new(Encoding.ASCII);
        private readonly byte[] buffer = new byte[65360];

        [Benchmark]
        public void TLD_SerializeUInt8()
        {
            this.eventBuilder.AddUInt8("Number", 123);
            this.eventBuilder.Reset("test");
        }

        [Benchmark]
        public void MsgPack_SerializeUInt8()
        {
            var cursor = MessagePackSerializer.SerializeAsciiString(this.buffer, 0, "Number");
            MessagePackSerializer.SerializeUInt8(this.buffer, cursor, 123);
        }

        [Benchmark]
        public void TLD_SerializeString()
        {
            this.eventBuilder.AddCountedString("name", "Span");
            this.eventBuilder.Reset("test");
        }

        [Benchmark]
        public void MsgPack_SerializeString()
        {
            var cursor = MessagePackSerializer.SerializeAsciiString(this.buffer, 0, "name");
            MessagePackSerializer.SerializeAsciiString(this.buffer, cursor, "Span");
        }

        [Benchmark]
        public void TLD_SerializeDateTime()
        {
            this.eventBuilder.AddFileTime("time", DateTime.UtcNow);
            this.eventBuilder.Reset("test");
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
