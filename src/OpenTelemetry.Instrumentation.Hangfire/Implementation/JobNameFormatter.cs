// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Hangfire;
using Hangfire.Common;

namespace OpenTelemetry.Instrumentation.Hangfire.Implementation;

internal static class JobNameFormatter
{
    private const string UnknownJobName = "UNKNOWN";

    internal static string FormatJobName(this Job? job)
    {
        if (job is null)
        {
            return UnknownJobName;
        }

        var sb = new StringBuilder()
            .Append(job.Type.ToGenericTypeString())
            .Append('.')
            .Append(job.Method.Name);

        return sb.ToString();
    }

    internal static string FormatJobName(this BackgroundJob backgroundJob) => backgroundJob.Job.FormatJobName();
}
