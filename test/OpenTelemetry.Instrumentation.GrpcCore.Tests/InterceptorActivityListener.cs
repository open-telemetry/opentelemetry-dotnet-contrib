// <copyright file="InterceptorActivityListener.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Linq;

namespace OpenTelemetry.Instrumentation.GrpcCore.Tests;

/// <summary>
/// This class listens for a single Activity created by the Grpc Core interceptors.
/// </summary>
internal sealed class InterceptorActivityListener : IDisposable
{
    /// <summary>
    /// The activity listener.
    /// </summary>
    private readonly ActivityListener activityListener;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterceptorActivityListener" /> class.
    /// </summary>
    /// <param name="activityIdentifier">The activity identifier.</param>
    public InterceptorActivityListener(Guid activityIdentifier)
    {
        this.activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == GrpcCoreInstrumentation.ActivitySourceName,
            ActivityStarted = activity =>
            {
                if (activity.TagObjects.Any(t => t.Key == SemanticConventions.AttributeActivityIdentifier && (Guid)t.Value == activityIdentifier))
                {
                    this.Activity = activity;
                }
            },
            Sample = this.Sample,
        };

        ActivitySource.AddActivityListener(this.activityListener);
        Debug.Assert(GrpcCoreInstrumentation.ActivitySource.HasListeners(), "activity source has no listeners");
    }

    /// <summary>
    /// Gets the started Activity.
    /// </summary>
    public Activity Activity { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.activityListener.Dispose();
    }

    /// <summary>
    /// Always sample.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <returns>a result.</returns>
    private ActivitySamplingResult Sample(ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded;
}
