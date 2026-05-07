// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

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
    /// <param name="testTags">The test activity tags.</param>
    public InterceptorActivityListener(TestActivityTags testTags)
    {
        this.activityListener = new ActivityListener
        {
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ShouldListenTo = static (source) => source.Name is "OpenTelemetry.Instrumentation.GrpcCore",
            ActivityStarted = (activity) =>
            {
                if (testTags.HasTestTags(activity))
                {
                    this.Activity = activity;
                }
            },
        };

        ActivitySource.AddActivityListener(this.activityListener);
    }

    /// <summary>
    /// Gets the started Activity.
    /// </summary>
    public Activity? Activity { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
        => this.activityListener.Dispose();
}
