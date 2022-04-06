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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Hangfire;
using Hangfire.MemoryStorage;
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

            var exportedItems = new List<Activity>();

            using var tel = Sdk.CreateTracerProviderBuilder()
                .AddHangfireInstrumentation()
                .AddInMemoryExporter(exportedItems)
                .Build();

            // Act
            BackgroundJob.Enqueue<TestJob>(x => x.Execute());

            using (var server = new BackgroundJobServer())
            {
                server.SendStop();
                server.WaitForShutdown(TimeSpan.FromSeconds(1));
            }

            // Assert
            Assert.Single(exportedItems);
            var activity = exportedItems.Single();
            Assert.Contains("JOB TestJob.Execute", activity.DisplayName);
            Assert.Equal(ActivityKind.Internal, activity.Kind);
        }
    }
}
