// <copyright file="QuartzDiagnosticListenerTests.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;
using Moq;
using OpenTelemetry.Trace;
using Quartz;
using Xunit;

namespace OpenTelemetry.Contrib.Instrumentation.Quartz.Tests
{
    public class QuartzDiagnosticListenerTests
    {
        private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(125);

        public QuartzDiagnosticListenerTests()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        }

        [Fact]
        public async Task Should_On_Job_Execute_Generate_Activity()
        {
            // Arrange
            Barrier barrier = new Barrier(2);
            List<DateTime> jobExecTimestamps = new List<DateTime>();

            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            using var tel = Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddQuartzInstrumentation()
                .AddProcessor(activityProcessor.Object)
                .Build();
            var schedulerConfig = SchedulerBuilder.Create("AUTO", "Scheduler");
            schedulerConfig.UseDefaultThreadPool(x => x.MaxConcurrency = 10);
            var scheduler = await schedulerConfig.BuildScheduler();

            scheduler.Context.Put("BARRIER", barrier);
            scheduler.Context.Put("DATESTAMPS", jobExecTimestamps);
            await scheduler.Start();

            JobDataMap jobDataMap = new JobDataMap { { "A", "B" } };

            var name = Guid.NewGuid().ToString();
            var job = JobBuilder.Create<TestJob>()
                .WithIdentity(name, SchedulerConstants.DefaultGroup)
                .UsingJobData(jobDataMap)
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity(name, SchedulerConstants.DefaultGroup)
                .StartNow()
                .Build();

            // Act
            await scheduler.ScheduleJob(job, trigger);

            barrier.SignalAndWait(TestTimeout);

            await scheduler.Shutdown(true);

            // Assert
            Assert.Equal(2, activityProcessor.Invocations.Count);
            var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];
            VerifyActivityData(activity);
        }

        private static void VerifyActivityData(Activity activity, bool isError = false)
        {
            Assert.Equal("Quartz.Job.Execute", activity.OperationName);
            Assert.Equal(ActivityKind.Internal, activity.Kind);
            Assert.Equal("Scheduler", activity.Tags.SingleOrDefault(t => t.Key.Equals("scheduler.name")).Value);
            Assert.Equal(SchedulerConstants.DefaultGroup, activity.Tags.SingleOrDefault(t => t.Key.Equals("job.group")).Value);
            Assert.Equal(SchedulerConstants.DefaultGroup, activity.Tags.SingleOrDefault(t => t.Key.Equals("trigger.group")).Value);
        }
    }
}
