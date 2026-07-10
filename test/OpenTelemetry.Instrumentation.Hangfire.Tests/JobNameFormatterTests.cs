// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Hangfire;
using Hangfire.Common;
using OpenTelemetry.Instrumentation.Hangfire.Implementation;

namespace OpenTelemetry.Instrumentation.Hangfire.Tests;

public class JobNameFormatterTests
{
    [Fact]
    public void FormatJobName_Returns_TypeAndMethod_When_Job_Is_Resolved()
    {
        var job = Job.FromExpression<TestJob>(x => x.Execute());
        var backgroundJob = new BackgroundJob("job-id", job, DateTime.UtcNow);

        var name = backgroundJob.FormatJobName();

        Assert.Equal("TestJob.Execute", name);
    }

    [Fact]
    public void FormatJobName_Does_Not_Throw_NullReferenceException_When_Job_Is_Unresolvable()
    {
        // null! mimics Hangfire's runtime state for a job whose definition failed to deserialize.
        var backgroundJob = new BackgroundJob("job-id", job: null!, DateTime.UtcNow);

        var exception = Record.Exception(() => backgroundJob.FormatJobName());

        Assert.False(exception is NullReferenceException, $"Expected no NullReferenceException, but got: {exception}");
    }
}
