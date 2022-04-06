// <copyright file="HangfireInstrumentationJobFilterAttributeTests.cs" company="OpenTelemetry Authors">
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
using Hangfire;
using Hangfire.MemoryStorage;
using Moq;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Hangfire.Tests
{
    public class HangfireInstrumentationJobFilterAttributeTests
    {
        [Fact]
        public void Should_Create_Activity()
        {
            // Arrange
            GlobalConfiguration.Configuration
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseMemoryStorage();

            var activityProcessor = new Mock<BaseProcessor<Activity>>();

            using var tel = Sdk.CreateTracerProviderBuilder()
                .AddHangfireInstrumentation()
                .AddProcessor(activityProcessor.Object)
                .Build();

            // Act
            BackgroundJob.Enqueue<TestJob>(x => x.Execute());

            using (var server = new BackgroundJobServer())
            {
                server.SendStop();
                server.WaitForShutdown(TimeSpan.FromSeconds(1));
            }

            // Assert
            Assert.Equal(1, activityProcessor.Invocations.Count);
            var activity = (Activity)activityProcessor.Invocations[0].Arguments[0];

            Assert.Contains("JOB TestJob.Execute", activity.DisplayName);
            Assert.Equal(ActivityKind.Internal, activity.Kind);
        }
    }
}
