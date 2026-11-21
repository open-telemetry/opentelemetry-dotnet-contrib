// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Hangfire;

namespace OpenTelemetry.Instrumentation.Hangfire.Implementation;

internal static class JobIdFormatter
{
    internal static string FormatJobId(this BackgroundJob backgroundJob)
    {
        var job = backgroundJob.Job;
        if (job is null)
        {
            return "UNKNOWN";
        }

        return $"{job.Type.FullName}.{job.Method.Name}";
    }
}
