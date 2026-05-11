// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

/// <summary>
/// Helper class with static methods for common WCF instrumentation test assertions.
/// </summary>
internal static class WcfTestHelpers
{
    internal const int MaxRetries = 5;

    public static Uri GetRandomBaseUri(string scheme)
        => new UriBuilder()
            {
                Scheme = scheme,
                Host = "localhost",
                Port = TcpPortProvider.GetOpenPort(),
                Path = "/",
            }.Uri;

    /// <summary>
    /// Asserts common activity properties for outgoing request instrumentation tests.
    /// </summary>
    /// <param name="activity">The activity to validate.</param>
    /// <param name="serviceBaseUri">The service base URI.</param>
    /// <param name="emptyOrNullAction">Whether the action name is empty or null.</param>
    /// <param name="includeVersion">Whether to validate SOAP message version.</param>
    /// <param name="includeVersionExpected">The expected version string when includeVersion is true (binding-specific).</param>
    /// <param name="schemeTag">The expected scheme tag value (e.g., "http" or "net.tcp").</param>
    /// <param name="enrich">Whether enrichment tags should be present.</param>
    /// <param name="enrichmentException">Whether enrichment threw an exception.</param>
    /// <param name="emitOldAttributes">Whether old RPC attributes are being emitted.</param>
    /// <param name="emitNewAttributes">Whether new RPC attributes are being emitted.</param>
    public static void AssertOutgoingRequestActivity(
        Activity activity,
        Uri serviceBaseUri,
        bool emptyOrNullAction,
        bool includeVersion,
        string? includeVersionExpected,
        string schemeTag,
        bool enrich,
        bool enrichmentException,
        bool emitOldAttributes,
        bool emitNewAttributes)
    {
        Assert.True(emitOldAttributes || emitNewAttributes, "No attributes are configured.");

        Assert.NotNull(activity);

        if (emptyOrNullAction)
        {
            Assert.Equal(WcfInstrumentationActivitySource.OutgoingRequestActivityName, activity.DisplayName);
            Assert.Equal("ExecuteWithEmptyActionName", activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeRpcMethod).Value);
        }
        else
        {
            Assert.Equal("http://opentelemetry.io/Service/Execute", activity.DisplayName);
            Assert.Equal("Execute", activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeRpcMethod).Value);
        }

        Assert.Equal(WcfInstrumentationActivitySource.OutgoingRequestActivityName, activity.OperationName);

        if (emitOldAttributes)
        {
            Assert.Equal(WcfInstrumentationConstants.WcfSystemValue, activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeRpcSystem).Value);
            Assert.Equal("http://opentelemetry.io/Service", activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeRpcService).Value);
            Assert.Equal(serviceBaseUri.Host, activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeNetPeerName).Value);
            Assert.Equal(serviceBaseUri.Port, activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeNetPeerPort).Value);
        }

        if (emitNewAttributes)
        {
            Assert.Equal(WcfInstrumentationConstants.WcfSystemValue, activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeRpcSystemName).Value);
            Assert.Equal(serviceBaseUri.Host, activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeServerAddress).Value);
            Assert.Equal(serviceBaseUri.Port, activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeServerPort).Value);
        }

        Assert.Equal(schemeTag, activity.TagObjects.FirstOrDefault(t => t.Key == WcfInstrumentationConstants.AttributeWcfChannelScheme).Value);
        Assert.Equal("/Service", activity.TagObjects.FirstOrDefault(t => t.Key == WcfInstrumentationConstants.AttributeWcfChannelPath).Value);

        if (includeVersion)
        {
            if (includeVersionExpected != null)
            {
                // HTTP binding: exact match
                Assert.Equal(includeVersionExpected, activity.TagObjects.FirstOrDefault(t => t.Key == WcfInstrumentationConstants.AttributeSoapMessageVersion).Value);
            }
            else
            {
                // TCP binding: regex match
                var value = activity.TagObjects.FirstOrDefault(t => t.Key == WcfInstrumentationConstants.AttributeSoapMessageVersion).Value!.ToString();
                Assert.Matches("""Soap.* \(http.*\) Addressing.* \(http.*\)""", value);
            }
        }

        if (enrich && !enrichmentException)
        {
            Assert.Equal(WcfEnrichEventNames.BeforeSendRequest, activity.TagObjects.Single(t => t.Key == "client.beforesendrequest").Value);
            Assert.Equal(WcfEnrichEventNames.AfterReceiveReply, activity.TagObjects.Single(t => t.Key == "client.afterreceivereply").Value);
        }

        Assert.Equal("OpenTelemetry.Instrumentation.Wcf", activity.Source.Name);
        Assert.NotNull(activity.Source.Version);
        Assert.NotEmpty(activity.Source.Version);

        if (emitOldAttributes && !emitNewAttributes)
        {
            Assert.Same(WcfInstrumentationActivitySource.ActivitySource, activity.Source);
            Assert.Equal("https://opentelemetry.io/schemas/1.23.0", activity.Source.TelemetrySchemaUrl);
        }
        else if (!emitOldAttributes && emitNewAttributes)
        {
            Assert.Same(WcfInstrumentationActivitySource.ActivitySourceNew, activity.Source);
            Assert.Equal("https://opentelemetry.io/schemas/1.41.0", activity.Source.TelemetrySchemaUrl);
        }
        else
        {
            Assert.Same(WcfInstrumentationActivitySource.ActivitySourceBoth, activity.Source);
            Assert.Null(activity.Source.TelemetrySchemaUrl);
        }
    }

#if NETFRAMEWORK
    public static void AssertIncomingRequestActivityCommon(
        Activity activity,
        Uri serviceBaseUri,
        bool emitOldAttributes,
        bool emitNewAttributes)
    {
        Assert.True(emitOldAttributes || emitNewAttributes, "No attributes are configured.");

        Assert.NotNull(activity);

        Assert.Equal(WcfInstrumentationActivitySource.IncomingRequestActivityName, activity.OperationName);

        if (emitOldAttributes)
        {
            Assert.Equal(WcfInstrumentationConstants.WcfSystemValue, activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeRpcSystem).Value);
            Assert.Equal("http://opentelemetry.io/Service", activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeRpcService).Value);
            Assert.Equal(serviceBaseUri.Host, activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeNetHostName).Value);
            Assert.Equal(serviceBaseUri.Port, activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeNetHostPort).Value);
        }

        if (emitNewAttributes)
        {
            Assert.Equal(WcfInstrumentationConstants.WcfSystemValue, activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeRpcSystemName).Value);
            Assert.Equal(serviceBaseUri.Host, activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeServerAddress).Value);
            Assert.Equal(serviceBaseUri.Port, activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeServerPort).Value);
        }

        Assert.Equal("net.tcp", activity.TagObjects.FirstOrDefault(t => t.Key == WcfInstrumentationConstants.AttributeWcfChannelScheme).Value);
        Assert.Equal("/Service", activity.TagObjects.FirstOrDefault(t => t.Key == WcfInstrumentationConstants.AttributeWcfChannelPath).Value);

        if (emitOldAttributes && !emitNewAttributes)
        {
            Assert.Same(WcfInstrumentationActivitySource.ActivitySource, activity.Source);
            Assert.Equal("https://opentelemetry.io/schemas/1.23.0", activity.Source.TelemetrySchemaUrl);
        }
        else if (!emitOldAttributes && emitNewAttributes)
        {
            Assert.Same(WcfInstrumentationActivitySource.ActivitySourceNew, activity.Source);
            Assert.Equal("https://opentelemetry.io/schemas/1.41.0", activity.Source.TelemetrySchemaUrl);
        }
        else
        {
            Assert.Same(WcfInstrumentationActivitySource.ActivitySourceBoth, activity.Source);
            Assert.Null(activity.Source.TelemetrySchemaUrl);
        }
    }
#endif

    public static void AssertDownstreamInstrumentationActivities(IList<Activity> stoppedActivities, bool filter)
    {
        Assert.NotEmpty(stoppedActivities);
        if (filter)
        {
            var activity = Assert.Single(stoppedActivities);
            Assert.Equal("DownstreamInstrumentation", activity.OperationName);
        }
        else
        {
            Assert.Equal(2, stoppedActivities.Count);
            Assert.NotNull(stoppedActivities.SingleOrDefault(a => a.OperationName == "DownstreamInstrumentation"));
            Assert.NotNull(stoppedActivities.SingleOrDefault(a => a.OperationName == WcfInstrumentationActivitySource.OutgoingRequestActivityName));
        }
    }

    /// <summary>
    /// Asserts that all activities have the same trace ID and that children have the parent as their parent.
    /// </summary>
    /// <param name="activities">The activities to validate.</param>
    public static void AssertActivitiesHaveCorrectParentage(IList<Activity> activities)
    {
        Assert.NotEmpty(activities);
        Assert.All(activities, activity => Assert.Equal(activities[0].TraceId, activity.TraceId));
        var parent = activities.Single(activity => activity.Parent == null);
        Assert.All(
            activities.Where(activity => activity != parent),
            activity => Assert.Equal(parent.SpanId, activity.ParentSpanId));
    }
}
