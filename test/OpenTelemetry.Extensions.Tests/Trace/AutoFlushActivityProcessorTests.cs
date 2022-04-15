// <copyright file="AutoFlushActivityProcessorTests.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using Moq;
using Moq.Protected;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Tests.Trace
{
    public class AutoFlushActivityProcessorTests
    {
        [Fact]
        public void AutoFlushActivityProcessor_FlushAfterLocalServerSideRootSpans_EndMatchingSpan_Flush()
        {
            var processor = new AutoFlushActivityProcessor(
                a => a.Parent == null && (a.Kind == ActivityKind.Server || a.Kind == ActivityKind.Consumer), 5000);

            var mockExporting = new Mock<BaseProcessor<Activity>>();

            using var provider = Sdk.CreateTracerProviderBuilder()
                .AddProcessor(mockExporting.Object)
                .AddProcessor(processor)
                .AddSource("foo")
                .Build();

            using var source = new ActivitySource("foo");
            using var activity = source.StartActivity("name", ActivityKind.Server);
            activity.Dispose();

            mockExporting.Protected().Verify("OnForceFlush", Times.Once(), 5_000);
        }

        [Fact]
        public void AutoFlushActivityProcessor_FlushAfterLocalServerSideRootSpans_EndNonMatchingSpan_DoesNothing()
        {
            var processor = new AutoFlushActivityProcessor(
                a => a.Parent == null && (a.Kind == ActivityKind.Server || a.Kind == ActivityKind.Consumer), 5000);

            var mockExporting = new Mock<BaseProcessor<Activity>>();

            using var provider = Sdk.CreateTracerProviderBuilder()
                .AddProcessor(mockExporting.Object)
                .AddProcessor(processor)
                .AddSource("foo")
                .Build();

            using var source = new ActivitySource("foo");
            using var activity = source.StartActivity("name", ActivityKind.Client);
            activity.Dispose();

            mockExporting.Protected().Verify("OnForceFlush", Times.Never(), It.IsAny<int>());
        }
    }
}
