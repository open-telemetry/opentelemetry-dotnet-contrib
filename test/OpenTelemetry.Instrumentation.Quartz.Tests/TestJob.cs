// <copyright file="TestJob.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;
using Quartz;

namespace OpenTelemetry.Instrumentation.Quartz.Tests
{
    public class TestJob : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            try
            {
                List<DateTime> jobExecTimestamps = (List<DateTime>)context.Scheduler.Context.Get("DATESTAMPS");
                Barrier barrier = (Barrier)context.Scheduler.Context.Get("BARRIER");

                jobExecTimestamps.Add(DateTime.UtcNow);

                barrier.SignalAndWait(TimeSpan.FromSeconds(125));
                return Task.CompletedTask;
            }
            catch (Exception e)
            {
                Console.Write(e);
            }

            return Task.CompletedTask;
        }
    }
}
