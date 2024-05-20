// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry.Trace;

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
                if (activity.TagObjects.Any(t => t.Key == Consts.TestActivityTag && (Guid)t.Value == activityIdentifier))
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
