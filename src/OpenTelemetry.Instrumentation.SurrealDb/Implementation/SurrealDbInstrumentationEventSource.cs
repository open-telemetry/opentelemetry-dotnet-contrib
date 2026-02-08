// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.SurrealDb.Implementation;

/// <summary>
/// EventSource events emitted from the project.
/// </summary>
[EventSource(Name = "OpenTelemetry-Instrumentation-SurrealDbClient")]
internal sealed class SurrealDbInstrumentationEventSource : EventSource, IConfigurationExtensionsLogger
{
    public static SurrealDbInstrumentationEventSource Log = new();

    [NonEvent]
    public void UnknownErrorProcessingEvent(string handlerName, string eventName, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.UnknownErrorProcessingEvent(handlerName, eventName, ex.ToInvariantString());
        }
    }

    [Event(
        1,
        Message = "Unknown error processing event '{1}' from handler '{0}', Exception: {2}",
        Level = EventLevel.Error
    )]
    public void UnknownErrorProcessingEvent(string handlerName, string eventName, string ex)
    {
        this.WriteEvent(1, handlerName, eventName, ex);
    }

    [Event(
        2,
        Message = "Current Activity is NULL in the '{0}' callback. Span will not be recorded.",
        Level = EventLevel.Warning
    )]
    public void NullActivity(string eventName)
    {
        this.WriteEvent(2, eventName);
    }

    [Event(
        3,
        Message = "Payload is NULL in event '{1}' from handler '{0}', span will not be recorded.",
        Level = EventLevel.Warning
    )]
    public void NullPayload(string handlerName, string eventName)
    {
        this.WriteEvent(3, handlerName, eventName);
    }

    [Event(6, Message = "Method is filtered out. Activity {0}", Level = EventLevel.Verbose)]
    public void MethodIsFilteredOut(string activityName)
    {
        this.WriteEvent(6, activityName);
    }

    [NonEvent]
    public void MethodFilterException(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.MethodFilterException(ex.ToInvariantString());
        }
    }

    [Event(
        7,
        Message = "Method filter threw exception. Method will not be collected. Exception {0}.",
        Level = EventLevel.Error
    )]
    public void MethodFilterException(string exception)
    {
        this.WriteEvent(7, exception);
    }

    [Event(
        8,
        Message = "Configuration key '{0}' has an invalid value: '{1}'",
        Level = EventLevel.Warning
    )]
    public void InvalidConfigurationValue(string key, string value)
    {
        this.WriteEvent(8, key, value);
    }

    void IConfigurationExtensionsLogger.LogInvalidConfigurationValue(string key, string value)
    {
        this.InvalidConfigurationValue(key, value);
    }
}
