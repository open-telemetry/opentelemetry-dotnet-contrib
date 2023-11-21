// <copyright file="HangfireInstrumentationOptions.cs" company="OpenTelemetry Authors">
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
using Hangfire;
using OpenTelemetry.Instrumentation.Hangfire.Implementation;

namespace OpenTelemetry.Trace;

/// <summary>
/// Options for Hangfire jobs instrumentation.
/// </summary>
public class HangfireInstrumentationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the exception will be recorded as ActivityEvent or not.
    /// </summary>
    /// <remarks>
    /// https://github.com/open-telemetry/semantic-conventions/blob/main/docs/exceptions/exceptions-spans.md.
    /// </remarks>
    public bool RecordException { get; set; }

    /// <summary>
    /// Gets or sets a delegate used to format the job name.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>{backgroundJob.Job.Type.Name}.{backgroundJob.Job.Method.Name}</c>.
    /// </remarks>
    public Func<BackgroundJob, string> DisplayNameFunc { get; set; } = HangfireInstrumentation.DefaultDisplayNameFunc;

    /// <summary>
    /// Gets or sets a filter function that determines whether or not to
    /// collect telemetry about the the <see cref="BackgroundJob"/> being executed.
    /// </summary>
    /// <remarks>
    /// <b>Notes:</b>
    /// <list type="bullet">
    /// <item>The first parameter passed to the filter function is <see cref="BackgroundJob"/> being executed.</item>
    /// <item>The return value for the filter:
    /// <list type="number">
    /// <item>If filter returns <see langword="true" />, the command is
    /// collected.</item>
    /// <item>If filter returns <see langword="false" /> or throws an
    /// exception, the command is <b>NOT</b> collected.</item>
    /// </list></item>
    /// </list>
    /// </remarks>
    public Func<BackgroundJob, bool>? Filter { get; set; }
}
