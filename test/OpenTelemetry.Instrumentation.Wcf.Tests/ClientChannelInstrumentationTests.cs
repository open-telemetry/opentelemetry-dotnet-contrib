// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.ServiceModel.Channels;
using OpenTelemetry.Instrumentation.Wcf.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

[Collection("WCF")]
public class ClientChannelInstrumentationTests
{
    [Fact]
    public void BeforeSendRequestSetsNetworkPeerTagsForIpLiteralRemoteAddress()
    {
        var stoppedActivities = new List<Activity>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stoppedActivities.Add,
        };
        ActivitySource.AddActivityListener(activityListener);

        WcfInstrumentationActivitySource.Options = new WcfInstrumentationOptions { EmitNewRpcAttributes = true };

        try
        {
            using var message = Message.CreateMessage(MessageVersion.Default, "http://opentelemetry.io/Service/Execute");

            var state = ClientChannelInstrumentation.BeforeSendRequest(message, new Uri("http://127.0.0.1:8080/Service"));
            state.Activity?.Stop();
            state.SuppressionScope?.Dispose();

            var activity = Assert.Single(stoppedActivities);
            Assert.Equal("127.0.0.1", activity.GetTagItem(SemanticConventions.AttributeNetworkPeerAddress));
            Assert.Equal(8080, activity.GetTagItem(SemanticConventions.AttributeNetworkPeerPort));
        }
        finally
        {
            WcfInstrumentationActivitySource.Options = null;
        }
    }
}
