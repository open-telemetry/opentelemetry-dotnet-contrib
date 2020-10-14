﻿// <copyright file="TestAWSXRayIdGenerator.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Contrib.Extensions.AWSRay.Tests
{
    public class TestAWSXRayIdGenerator
    {
        [Fact]
        public void TestGenerateTraceIdForRootNode()
        {
            var activity = new Activity("Test");
            var originalTraceId = activity.TraceId;
            var originalParentSpanId = activity.ParentSpanId;
            var originalTraceFlag = activity.ActivityTraceFlags;

            using (Sdk.CreateTracerProviderBuilder().AddXRayActivityTraceIdGenerator().Build())
            {
                activity.Start();

                Assert.NotEqual(originalTraceId, activity.TraceId);
                Assert.NotEqual(originalParentSpanId, activity.ParentSpanId);
                Assert.Equal("0000000000000000", activity.ParentSpanId.ToHexString());
                Assert.Equal(originalTraceFlag, activity.ActivityTraceFlags);
            }
        }

        [Fact]
        public void TestGenerateTraceIdForNonRootNodeSampled()
        {
            var activity = new Activity("Test");
            var traceId = ActivityTraceId.CreateFromString("12345678901234567890123456789012".AsSpan());
            var parentId = ActivitySpanId.CreateFromString("1234567890123456".AsSpan());
            activity.SetParentId(traceId, parentId, ActivityTraceFlags.Recorded);

            using (Sdk.CreateTracerProviderBuilder().AddXRayActivityTraceIdGenerator().Build())
            {
                activity.Start();

                Assert.Equal("12345678901234567890123456789012", activity.TraceId.ToHexString());
                Assert.Equal("1234567890123456", activity.ParentSpanId.ToHexString());
                Assert.Equal(ActivityTraceFlags.Recorded, activity.ActivityTraceFlags);
            }
        }

        [Fact]
        public void TestGenerateTraceIdForNonRootNodeNotSampled()
        {
            var activity = new Activity("Test");
            var traceId = ActivityTraceId.CreateFromString("12345678901234567890123456789012".AsSpan());
            var parentId = ActivitySpanId.CreateFromString("1234567890123456".AsSpan());
            activity.SetParentId(traceId, parentId, ActivityTraceFlags.None);

            using (Sdk.CreateTracerProviderBuilder().AddXRayActivityTraceIdGenerator().Build())
            {
                activity.Start();

                Assert.Equal("12345678901234567890123456789012", activity.TraceId.ToHexString());
                Assert.Equal("1234567890123456", activity.ParentSpanId.ToHexString());
                Assert.Equal(ActivityTraceFlags.None, activity.ActivityTraceFlags);
            }
        }
    }
}
