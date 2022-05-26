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
