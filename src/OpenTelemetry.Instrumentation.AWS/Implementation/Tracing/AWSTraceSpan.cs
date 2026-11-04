// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Runtime.Telemetry;
using Amazon.Runtime.Telemetry.Tracing;

namespace OpenTelemetry.Instrumentation.AWS.Implementation.Tracing;

internal sealed class AWSTraceSpan : TraceSpan
{
    private readonly Activity? activity;
    private readonly bool recordException;

    public AWSTraceSpan(Activity? activity, bool recordException)
    {
        this.activity = activity;
        this.recordException = recordException;
        this.Name = activity?.DisplayName;
    }

    internal static AWSTraceSpan Noop { get; } = new(null, recordException: false);

    public override void EmitEvent(string name, Attributes? attributes = null)
    {
        if (this.activity == null || !this.activity.IsAllDataRequested)
        {
            return;
        }

        var tags = attributes != null ? new ActivityTagsCollection(attributes.AllAttributes) : null;
        this.activity.AddEvent(new ActivityEvent(name, tags: tags));
    }

    public override void SetAttribute(string key, object value)
    {
        if (this.activity == null || !this.activity.IsAllDataRequested)
        {
            return;
        }

        // The AWS SDK also copies the message and stack trace of the exception
        // of a failed call into span attributes. As these may contain
        // sensitive values quoted back by the service, only keep them when opted in.
        if (!this.recordException &&
            (string.Equals(key, TelemetryConstants.ExceptionMessageAttributeKey, StringComparison.Ordinal) ||
             string.Equals(key, TelemetryConstants.ExceptionStackTraceAttributeKey, StringComparison.Ordinal)))
        {
            return;
        }

        this.activity.SetTag(key, value);
    }

    public override void SetStatus(SpanStatus status)
    {
        if (this.activity == null || !this.activity.IsAllDataRequested)
        {
            return;
        }

        var activityStatus = ConvertToActivityStatusCode(status);
        this.activity.SetStatus(activityStatus);
    }

    public override void RecordException(Exception exception, Attributes? attributes = null)
    {
        if (this.activity == null || !this.activity.IsAllDataRequested)
        {
            return;
        }

        if (this.recordException)
        {
            var tags = attributes != null ? new TagList(attributes.AllAttributes.ToArray()) : default;
            this.activity.AddException(exception, tags);
        }

        this.activity.SetStatus(ActivityStatusCode.Error);
    }

    public override void End()
    {
        this.activity?.Stop();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.End();
            this.activity?.Dispose();
        }

        base.Dispose(disposing);
    }

    private static ActivityStatusCode ConvertToActivityStatusCode(SpanStatus status)
    {
        return status switch
        {
            SpanStatus.OK => ActivityStatusCode.Ok,
            SpanStatus.ERROR => ActivityStatusCode.Error,
            SpanStatus.UNSET => ActivityStatusCode.Unset,
            _ => ActivityStatusCode.Unset,
        };
    }
}
