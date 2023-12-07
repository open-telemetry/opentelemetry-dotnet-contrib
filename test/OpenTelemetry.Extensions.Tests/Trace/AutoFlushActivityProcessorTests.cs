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
using System.Runtime.CompilerServices;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Tests.Trace;

public class AutoFlushActivityProcessorTests
{
    // [Fact(Skip = "Unstable")]
    [Fact]
    public void AutoFlushActivityProcessor_FlushAfterLocalServerSideRootSpans_EndMatchingSpan_Flush()
    {
        int invocations = 0;
        var activityProcessor = new TestActivityProcessor
        {
            ForceFlushed = () => invocations++,
        };
        var sourceName = GetTestMethodName();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor)
            .AddAutoFlushActivityProcessor(a => a.Parent == null && (a.Kind == ActivityKind.Server || a.Kind == ActivityKind.Consumer), 5000)
            .AddSource(sourceName)
            .Build();

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Server);
        Assert.NotNull(activity);
        activity.Stop();

        Assert.Equal(1, invocations);
    }

    [Fact]
    public void AutoFlushActivityProcessor_FlushAfterLocalServerSideRootSpans_EndNonMatchingSpan_DoesNothing()
    {
        int invocations = 0;
        var activityProcessor = new TestActivityProcessor
        {
            ForceFlushed = () => invocations++,
        };
        var sourceName = GetTestMethodName();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor)
            .AddAutoFlushActivityProcessor(a => a.Parent == null && (a.Kind == ActivityKind.Server || a.Kind == ActivityKind.Consumer))
            .AddSource(sourceName)
            .Build();

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Client);
        Assert.NotNull(activity);
        activity.Stop();

        Assert.Equal(0, invocations);
    }

    [Fact]
    public void AutoFlushActivityProcessor_PredicateThrows_DoesNothing()
    {
        int invocations = 0;
        var activityProcessor = new TestActivityProcessor
        {
            ForceFlushed = () => invocations++,
        };
        var sourceName = GetTestMethodName();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor)
            .AddAutoFlushActivityProcessor(_ => throw new Exception("Predicate throws an exception."))
            .AddSource(sourceName)
            .Build();

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Server);
        Assert.NotNull(activity);
        activity.Stop();

        Assert.Equal(0, invocations);
    }

    private static string GetTestMethodName([CallerMemberName] string callingMethodName = "")
    {
        return callingMethodName;
    }
}
