// <copyright file="TraceProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Instrumentation.Quartz;
using OpenTelemetry.Instrumentation.Quartz.Implementation;

// ReSharper disable once CheckNamespace
namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static class TraceProviderBuilderExtensions
{
    /// <summary>
    /// Enables the Quartz.NET Job automatic data collection for Quartz.NET.
    /// </summary>
    /// <param name="builder"><see cref="TraceProviderBuilderExtensions"/> being configured.</param>
    /// <param name="configureQuartzInstrumentationOptions">Quartz configuration options.</param>
    /// <returns>The instance of <see cref="TraceProviderBuilderExtensions"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddQuartzInstrumentation(
        this TracerProviderBuilder builder,
        Action<QuartzInstrumentationOptions> configureQuartzInstrumentationOptions = null)
    {
        var options = new QuartzInstrumentationOptions();
        configureQuartzInstrumentationOptions?.Invoke(options);

        builder.AddInstrumentation(() => new QuartzJobInstrumentation(options));
        builder.AddSource(QuartzDiagnosticListener.ActivitySourceName);

        builder.AddLegacySource(OperationName.Job.Execute);
        builder.AddLegacySource(OperationName.Job.Veto);

        return builder;
    }
}
