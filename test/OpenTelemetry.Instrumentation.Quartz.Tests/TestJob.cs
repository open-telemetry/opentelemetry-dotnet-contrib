// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Quartz;

namespace OpenTelemetry.Instrumentation.Quartz.Tests;

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
