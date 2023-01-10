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
using OpenTelemetry.Exporter.Geneva.External;
using OpenTelemetry.Exporter.Geneva.TldExporter;

/*
BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.22621.963)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=7.0.101
  [Host]     : .NET 7.0.1 (7.0.122.56804), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.1 (7.0.122.56804), X64 RyuJIT AVX2


|                         Method |      Mean |     Error |    StdDev | Allocated |
|------------------------------- |----------:|----------:|----------:|----------:|
|             TLD_SerializeUInt8 | 22.483 ns | 0.0216 ns | 0.0202 ns |         - |
|         MsgPack_SerializeUInt8 |  7.360 ns | 0.0135 ns | 0.0127 ns |         - |
|       TLD_SerializeAsciiString | 31.141 ns | 0.0210 ns | 0.0176 ns |         - |
|   MsgPack_SerializeAsciiString | 19.580 ns | 0.0412 ns | 0.0385 ns |         - |
|  TLD_SerializeUnicodeSubString | 41.064 ns | 0.0708 ns | 0.0662 ns |         - |
|     TLD_SerializeUnicodeString | 41.889 ns | 0.0927 ns | 0.0868 ns |         - |
| MsgPack_SerializeUnicodeString | 21.806 ns | 0.0281 ns | 0.0249 ns |         - |
|          TLD_SerializeDateTime | 45.321 ns | 0.1321 ns | 0.1235 ns |         - |
|      MsgPack_SerializeDateTime | 30.667 ns | 0.0401 ns | 0.0356 ns |         - |
|                      TLD_Reset | 12.739 ns | 0.0351 ns | 0.0328 ns |         - |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmark;

[MemoryDiagnoser]
public class SerializationBenchmarks
{
    private const int StringLengthLimit = (1 << 14) - 1;
    private const string Key = "ext_dt_traceId";
    private const string Value = "e8ea7e9ac72de94e91fabc613f9686b2";
    private readonly EventBuilder eventBuilder = new(UncheckedASCIIEncoding.SharedInstance);
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
        this.eventBuilder.AddCountedString(Key, Value);
    }

    [Benchmark]
    public void MsgPack_SerializeAsciiString()
    {
        var cursor = MessagePackSerializer.SerializeAsciiString(this.buffer, 0, Key);
        MessagePackSerializer.SerializeAsciiString(this.buffer, cursor, Value);
    }

    [Benchmark]
    public void TLD_SerializeUnicodeSubString()
    {
        this.eventBuilder.Reset("test");
        this.eventBuilder.AddCountedAnsiString(Key, Value, Encoding.UTF8, 0, Math.Min(Value.Length, StringLengthLimit));
    }

    [Benchmark]
    public void TLD_SerializeUnicodeString()
    {
        this.eventBuilder.Reset("test");
        this.eventBuilder.AddCountedAnsiString(Key, Value, Encoding.UTF8);
    }

    [Benchmark]
    public void MsgPack_SerializeUnicodeString()
    {
        var cursor = MessagePackSerializer.SerializeAsciiString(this.buffer, 0, Key);
        MessagePackSerializer.SerializeUnicodeString(this.buffer, cursor, Value);
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
