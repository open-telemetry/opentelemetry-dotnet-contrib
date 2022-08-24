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
|       TLD_SerializeUInt32 | 39.55 ns | 0.155 ns | 0.145 ns |         - |
|   MsgPack_SerializeUInt32 | 10.39 ns | 0.073 ns | 0.069 ns |         - |
|       TLD_SerializeString | 59.26 ns | 0.636 ns | 0.595 ns |         - |
|   MsgPack_SerializeString | 19.04 ns | 0.048 ns | 0.040 ns |         - |
|     TLD_SerializeDateTime | 62.31 ns | 0.245 ns | 0.229 ns |         - |
| MsgPack_SerializeDateTime | 36.38 ns | 0.222 ns | 0.186 ns |         - |
|                 TLD_Reset | 21.11 ns | 0.030 ns | 0.025 ns |         - |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmark.Exporter
{
    [MemoryDiagnoser]
    public class SerializationBenchmarks
    {
        private readonly EventBuilder eventBuilder = new();
        private readonly EventBuilder eventBuilderForWrite = new();
        private readonly EventProvider eventProvider = new("OpenTelemetry");
        private readonly byte[] buffer = new byte[65360];

        public SerializationBenchmarks()
        {
            this.eventBuilderForWrite.AddUInt16("__csver__", 1024, EventOutType.Hex);
            this.eventBuilderForWrite.AddCountedString("String", "text");
            this.eventBuilderForWrite.AddUInt32("Number", 123);
            this.eventBuilderForWrite.AddFileTime("time", DateTime.UtcNow, EventOutType.DateTimeUtc);
        }

        [Benchmark]
        public void TLD_SerializeUInt32()
        {
            this.eventBuilder.AddUInt32("Number", 123);
            this.eventBuilder.Reset("test");
        }

        [Benchmark]
        public void MsgPack_SerializeUInt32()
        {
            var cursor = MessagePackSerializer.SerializeAsciiString(this.buffer, 0, "Number");
            MessagePackSerializer.SerializeUInt32(this.buffer, cursor, 123);
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
            this.eventBuilder.AddFileTime("time", DateTime.UtcNow, EventOutType.DateTimeUtc);
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
