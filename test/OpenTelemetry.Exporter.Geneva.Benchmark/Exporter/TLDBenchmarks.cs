// <copyright file="TLDBenchmarks.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Geneva.Benchmark.Exporter
{
    [MemoryDiagnoser]
    public class TLDBenchmarks
    {
        private readonly EventBuilder eventBuilder = new();
        private readonly EventBuilder eventBuilder2 = new();
        private readonly EventProvider eventProvider = new("OpenTelemetry");

        public TLDBenchmarks()
        {
            this.eventBuilder.AddUInt16("__csver__", 1024, EventOutType.Hex);
            this.eventBuilder.AddCountedString("String", "text");
            this.eventBuilder2.AddUInt32("Number", 123);
            this.eventBuilder.AddFileTime("time", DateTime.UtcNow, EventOutType.DateTimeUtc);
        }

        [Benchmark]
        public void SerializeString()
        {
            this.eventBuilder.AddCountedString("name", "Span");
            this.eventBuilder.Reset("test");
        }

        [Benchmark]
        public void Reset()
        {
            this.eventBuilder.Reset("test");
        }

        [Benchmark]
        public void Write()
        {
            this.eventProvider.Write(this.eventBuilder2);
        }

        [Benchmark]
        public bool IsEnabled()
        {
            return this.eventProvider.IsEnabled();
        }
    }
}
