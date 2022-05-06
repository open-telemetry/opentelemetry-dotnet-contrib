// <copyright file="InstanaSpan.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("OpenTelemetry.Exporter.Instana.Tests, PublicKey=002400000480000094000000060200000024000052534131000400000100010051c1562a090fb0c9f391012a32198b5e5d9a60e9b80fa2d7b434c9e5ccb7259bd606e66f9660676afc6692b8cdc6793d190904551d2103b7b22fa636dcbb8208839785ba402ea08fc00c8f1500ccef28bbf599aa64ffb1e1d5dc1bf3420a3777badfe697856e9d52070a50c3ea5821c80bef17ca3acffa28f89dd413f096f898")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2, PublicKey = 0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7")]

namespace OpenTelemetry.Exporter.Instana.Implementation
{
    internal enum SpanKind
    {
#pragma warning disable SA1602 // Enumeration items should be documented
        ENTRY,
#pragma warning restore SA1602 // Enumeration items should be documented
#pragma warning disable SA1602 // Enumeration items should be documented
        EXIT,
#pragma warning restore SA1602 // Enumeration items should be documented
#pragma warning disable SA1602 // Enumeration items should be documented
        INTERMEDIATE,
#pragma warning restore SA1602 // Enumeration items should be documented
#pragma warning disable SA1602 // Enumeration items should be documented
        NOT_SET,
#pragma warning restore SA1602 // Enumeration items should be documented
    }

    internal class InstanaSpan
    {
        public InstanaSpanTransformInfo TransformInfo { get; set; }

        public string N { get; internal set; }

        public string T { get; internal set; }

        public string Lt { get; internal set; }

        public From F { get; internal set; }

        public string P { get; internal set; }

        public string S { get; internal set; }

        public SpanKind K { get; internal set; }

        public Data Data { get; internal set; }

        public long Ts { get; internal set; }

        public long D { get; internal set; }

        public bool Tp { get; internal set; }

        public int Ec { get; internal set; }
    }

#pragma warning disable SA1402 // File may only contain a single type
    internal class From
#pragma warning restore SA1402 // File may only contain a single type
    {
        public string E { get; internal set; }

        public string H { get; internal set; }
    }

#pragma warning disable SA1402 // File may only contain a single type
    internal class Data
#pragma warning restore SA1402 // File may only contain a single type
    {
#pragma warning disable SA1300 // Element should begin with upper-case letter
        public Dictionary<string, object> data { get; internal set; }

#pragma warning restore SA1300 // Element should begin with upper-case letter
        public Dictionary<string, string> Tags { get; internal set; }

        public List<SpanEvent> Events { get; internal set; }
    }

#pragma warning disable SA1402 // File may only contain a single type
    internal class SpanEvent
#pragma warning restore SA1402 // File may only contain a single type
    {
        public string Name { get; internal set; }

        public long Ts { get; internal set; }

        public Dictionary<string, string> Tags { get; internal set; }
    }
}
