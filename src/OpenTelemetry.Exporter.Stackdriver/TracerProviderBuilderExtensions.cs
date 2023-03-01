// <copyright file="TracerProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Exporter.Stackdriver;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering a Stackdriver exporter.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Registers a Stackdriver exporter that will receive <see cref="System.Diagnostics.Activity"/> instances.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="projectId">Project ID to send telemetry to.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder UseStackdriverExporter(
        this TracerProviderBuilder builder,
        string projectId)
    {
        Guard.ThrowIfNull(builder);

#pragma warning disable CA2000 // Dispose objects before losing scope
        var activityExporter = new StackdriverTraceExporter(projectId);

        return builder.AddProcessor(new BatchActivityExportProcessor(activityExporter));
#pragma warning restore CA2000 // Dispose objects before losing scope
    }
}
