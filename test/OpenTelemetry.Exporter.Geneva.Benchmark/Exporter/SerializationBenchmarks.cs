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
using OpenTelemetry.Exporter.Geneva.TLDExporter;

/*
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=7.0.100-preview.6.22352.1
  [Host]     : .NET 6.0.8 (6.0.822.36306), X64 RyuJIT
  DefaultJob : .NET 6.0.8 (6.0.822.36306), X64 RyuJIT


|                         Method |     Mean |    Error |   StdDev | Allocated |
|------------------------------- |---------:|---------:|---------:|----------:|
|             TLD_SerializeUInt8 | 23.40 ns | 0.142 ns | 0.126 ns |         - |
|         MsgPack_SerializeUInt8 | 10.83 ns | 0.031 ns | 0.029 ns |         - |
|       TLD_SerializeAsciiString | 31.15 ns | 0.043 ns | 0.038 ns |         - |
|   MsgPack_SerializeAsciiString | 23.85 ns | 0.040 ns | 0.031 ns |         - |
|  TLD_SerializeUnicodeSubString | 43.14 ns | 0.067 ns | 0.062 ns |         - |
|     TLD_SerializeUnicodeString | 43.57 ns | 0.081 ns | 0.076 ns |         - |
| MsgPack_SerializeUnicodeString | 27.78 ns | 0.017 ns | 0.015 ns |         - |
|          TLD_SerializeDateTime | 49.22 ns | 0.027 ns | 0.024 ns |         - |
|      MsgPack_SerializeDateTime | 35.95 ns | 0.051 ns | 0.043 ns |         - |
|                      TLD_Reset | 12.40 ns | 0.006 ns | 0.005 ns |         - |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmark.Exporter;

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
