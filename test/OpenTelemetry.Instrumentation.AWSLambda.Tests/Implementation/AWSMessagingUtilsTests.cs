// <copyright file="AWSMessagingUtilsTests.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Amazon.Lambda.SQSEvents;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.AWSLambda.Implementation;
using OpenTelemetry.Trace;
using Xunit;
using static Amazon.Lambda.SQSEvents.SQSEvent;

namespace OpenTelemetry.Instrumentation.AWSLambda.Tests.Implementation;

[Collection("TracerProviderDependent")]
public class AWSMessagingUtilsTests : IDisposable
{
    private const string TraceId = "0af7651916cd43dd8448eb211c80319c";
    private const string SpanId1 = "b9c7c989f97918e1";
    private const string SpanId2 = "b9c7c989f97918e2";

    private readonly TracerProvider tracerProvider;

    public AWSMessagingUtilsTests()
    {
        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .Build();
    }

    public void Dispose()
    {
        this.tracerProvider.Dispose();
    }

    [Fact]
    public void ExtractParentContext_SetParentFromMessageBatchIsDisabled_ParentIsNotSet()
    {
        AWSMessagingUtils.SetParentFromMessageBatch = false;
        var sqsEvent = CreateSqsEventWithMessages(new[] { SpanId1, SpanId2 });

        (PropagationContext parentContext, IEnumerable<ActivityLink> links) = AWSMessagingUtils.ExtractParentContext(sqsEvent);

        Assert.Equal(default, parentContext);
        Assert.Equal(2, links.Count());
    }

    [Fact]
    public void ExtractParentContext_SetParentFromMessageBatchIsEnabled_ParentIsSetFromLastMessage()
    {
        AWSMessagingUtils.SetParentFromMessageBatch = true;
        var sqsEvent = CreateSqsEventWithMessages(new[] { SpanId1, SpanId2 });

        (PropagationContext parentContext, IEnumerable<ActivityLink> links) = AWSMessagingUtils.ExtractParentContext(sqsEvent);

        Assert.NotEqual(default, parentContext);
        Assert.Equal(SpanId2, parentContext.ActivityContext.SpanId.ToHexString());
        Assert.Equal(2, links.Count());
    }

    private static SQSEvent CreateSqsEventWithMessages(string[] spans)
    {
        var @event = new SQSEvent { Records = new List<SQSMessage>() };
        for (var i = 0; i < spans.Length; i++)
        {
            var message = new SQSMessage { MessageAttributes = new Dictionary<string, MessageAttribute>() };
            message.MessageAttributes.Add("traceparent", new MessageAttribute { StringValue = $"00-{TraceId}-{spans[i]}-01" });
            message.MessageAttributes.Add("tracestate", new MessageAttribute { StringValue = $"k1=v1,k2=v2" });
            @event.Records.Add(message);
        }

        return @event;
    }
}
