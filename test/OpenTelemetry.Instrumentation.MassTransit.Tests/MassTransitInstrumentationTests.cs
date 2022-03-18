// <copyright file="MassTransitInstrumentationTests.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MassTransit.Testing;
using Moq;
using OpenTelemetry.Instrumentation.MassTransit.Implementation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.MassTransit.Tests
{
    public class MassTransitInstrumentationTests
    {
        [Fact]
        public async Task ShouldMapMassTransitTagsForPublishMessageToOpenTelemetrySpecification()
        {
            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            using (Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .AddMassTransitInstrumentation()
                .Build())
            {
                var harness = new InMemoryTestHarness();
                var consumerHarness = harness.Consumer<TestConsumer>();
                var handlerHarness = harness.Handler<TestMessage>();
                await harness.Start();
                try
                {
                    await harness.InputQueueSendEndpoint.Send<TestMessage>(new { Text = "Hello, world!" });

                    Assert.True(await harness.Consumed.SelectAsync<TestMessage>().Any());
                    Assert.True(await consumerHarness.Consumed.SelectAsync<TestMessage>().Any());
                    Assert.True(await handlerHarness.Consumed.SelectAsync().Any());
                }
                finally
                {
                    await harness.Stop();
                }

                var expectedMessageContext = harness.Sent.Select<TestMessage>().FirstOrDefault()?.Context;
                var actualActivity = this.GetActivitiesFromInvocationsByOperationName(activityProcessor.Invocations, OperationName.Transport.Send).LastOrDefault();

                Assert.NotNull(actualActivity);
                Assert.NotNull(expectedMessageContext);

                Assert.Equal("/input_queue send", actualActivity.DisplayName);
                Assert.Equal(ActivityKind.Producer, actualActivity.Kind);
                Assert.Equal("loopback", actualActivity.GetTagValue(SemanticConventions.AttributeMessagingSystem)?.ToString());

                Assert.Equal(expectedMessageContext.MessageId.ToString(), actualActivity.GetTagValue(SemanticConventions.AttributeMessagingMessageId)?.ToString());
                Assert.Equal(expectedMessageContext.ConversationId.ToString(), actualActivity.GetTagValue(SemanticConventions.AttributeMessagingConversationId)?.ToString());
                Assert.Equal(expectedMessageContext.DestinationAddress.AbsolutePath, actualActivity.GetTagValue(SemanticConventions.AttributeMessagingDestination)?.ToString());
                Assert.Equal(expectedMessageContext.DestinationAddress.Host, actualActivity.GetTagValue(SemanticConventions.AttributeNetPeerName)?.ToString());

                Assert.Null(actualActivity.GetTagValue(TagName.MessageId));
                Assert.Null(actualActivity.GetTagValue(TagName.ConversationId));
                Assert.Null(actualActivity.GetTagValue(TagName.DestinationAddress));

                Assert.Null(actualActivity.GetTagValue(TagName.SpanKind));
                Assert.Null(actualActivity.GetTagValue(TagName.PeerService));

                Assert.Null(actualActivity.GetTagValue(TagName.PeerAddress));
                Assert.Null(actualActivity.GetTagValue(TagName.PeerHost));
                Assert.Null(actualActivity.GetTagValue(TagName.SourceAddress));
            }
        }

        [Fact]
        public async Task ShouldMapMassTransitTagsForReceiveMessageToOpenTelemetrySpecification()
        {
            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            using (Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .AddMassTransitInstrumentation()
                .Build())
            {
                var harness = new InMemoryTestHarness();
                var consumerHarness = harness.Consumer<TestConsumer>();
                var handlerHarness = harness.Handler<TestMessage>();
                await harness.Start();
                try
                {
                    await harness.InputQueueSendEndpoint.Send<TestMessage>(new { Text = "Hello, world!" });

                    Assert.True(await harness.Consumed.SelectAsync<TestMessage>().Any());
                    Assert.True(await consumerHarness.Consumed.SelectAsync<TestMessage>().Any());
                    Assert.True(await handlerHarness.Consumed.SelectAsync().Any());
                }
                finally
                {
                    await harness.Stop();
                }

                var expectedMessageContext = harness.Sent.Select<TestMessage>().FirstOrDefault()?.Context;
                var actualActivity = this.GetActivitiesFromInvocationsByOperationName(activityProcessor.Invocations, OperationName.Transport.Receive).LastOrDefault();

                Assert.NotNull(actualActivity);
                Assert.NotNull(expectedMessageContext);

                Assert.Equal("/input_queue consume", actualActivity.DisplayName);
                Assert.Equal(ActivityKind.Consumer, actualActivity.Kind);
                Assert.Equal("loopback", actualActivity.GetTagValue(SemanticConventions.AttributeMessagingSystem)?.ToString());

                Assert.Equal(expectedMessageContext.MessageId.ToString(), actualActivity.GetTagValue(SemanticConventions.AttributeMessagingMessageId)?.ToString());
                Assert.Equal(expectedMessageContext.ConversationId.ToString(), actualActivity.GetTagValue(SemanticConventions.AttributeMessagingConversationId)?.ToString());
                Assert.Equal(expectedMessageContext.DestinationAddress.AbsolutePath, actualActivity.GetTagValue(SemanticConventions.AttributeMessagingDestination)?.ToString());
                Assert.Equal(expectedMessageContext.DestinationAddress.Host, actualActivity.GetTagValue(SemanticConventions.AttributeNetPeerName)?.ToString());

                Assert.Null(actualActivity.GetTagValue(TagName.MessageId));
                Assert.Null(actualActivity.GetTagValue(TagName.ConversationId));
                Assert.Null(actualActivity.GetTagValue(TagName.DestinationAddress));

                Assert.Null(actualActivity.GetTagValue(TagName.SpanKind));
                Assert.Null(actualActivity.GetTagValue(TagName.PeerService));

                Assert.Null(actualActivity.GetTagValue(TagName.PeerAddress));
                Assert.Null(actualActivity.GetTagValue(TagName.PeerHost));
                Assert.Null(actualActivity.GetTagValue(TagName.MessageTypes));
                Assert.Null(actualActivity.GetTagValue(TagName.SourceAddress));
                Assert.Null(actualActivity.GetTagValue(TagName.SourceHostMachine));
            }
        }

        [Fact]
        public async Task ShouldMapMassTransitTagsForConsumeMessageToOpenTelemetrySpecification()
        {
            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            using (Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .AddMassTransitInstrumentation()
                .Build())
            {
                var harness = new InMemoryTestHarness();
                var consumerHarness = harness.Consumer<TestConsumer>();
                var handlerHarness = harness.Handler<TestMessage>();
                await harness.Start();
                try
                {
                    await harness.InputQueueSendEndpoint.Send<TestMessage>(new { Text = "Hello, world!" });

                    Assert.True(await harness.Consumed.SelectAsync<TestMessage>().Any());
                    Assert.True(await consumerHarness.Consumed.SelectAsync<TestMessage>().Any());
                    Assert.True(await handlerHarness.Consumed.SelectAsync().Any());
                }
                finally
                {
                    await harness.Stop();
                }

                var expectedMessageContext = harness.Sent.Select<TestMessage>().FirstOrDefault()?.Context;
                var actualActivity = this.GetActivitiesFromInvocationsByOperationName(activityProcessor.Invocations, OperationName.Consumer.Consume).LastOrDefault();

                Assert.NotNull(actualActivity);
                Assert.NotNull(expectedMessageContext);
                Assert.Equal("OpenTelemetry.Instrumentation.MassTransit.Tests.TestConsumer process", actualActivity.DisplayName);
                Assert.Equal(ActivityKind.Internal, actualActivity.Kind);
                Assert.Equal("OpenTelemetry.Instrumentation.MassTransit.Tests.TestConsumer", actualActivity.GetTagValue(MassTransitSemanticConventions.AttributeMessagingMassTransitConsumerType)?.ToString());

                Assert.Null(actualActivity.GetTagValue(TagName.SpanKind));
                Assert.Null(actualActivity.GetTagValue(TagName.PeerService));

                Assert.Null(actualActivity.GetTagValue(TagName.PeerAddress));
                Assert.Null(actualActivity.GetTagValue(TagName.PeerHost));
            }
        }

        [Fact]
        public async Task ShouldMapMassTransitTagsForHandleMessageToOpenTelemetrySpecification()
        {
            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            using (Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .AddMassTransitInstrumentation()
                .Build())
            {
                var harness = new InMemoryTestHarness();
                var consumerHarness = harness.Consumer<TestConsumer>();
                var handlerHarness = harness.Handler<TestMessage>();
                await harness.Start();
                try
                {
                    await harness.InputQueueSendEndpoint.Send<TestMessage>(new { Text = "Hello, world!" });

                    Assert.True(await harness.Consumed.SelectAsync<TestMessage>().Any());
                    Assert.True(await consumerHarness.Consumed.SelectAsync<TestMessage>().Any());
                    Assert.True(await handlerHarness.Consumed.SelectAsync().Any());
                }
                finally
                {
                    await harness.Stop();
                }

                var expectedMessageContext = harness.Sent.Select<TestMessage>().FirstOrDefault()?.Context;
                var actualActivity = this.GetActivitiesFromInvocationsByOperationName(activityProcessor.Invocations, OperationName.Consumer.Handle).LastOrDefault();

                Assert.NotNull(actualActivity);
                Assert.NotNull(expectedMessageContext);
                Assert.Equal("TestMessage/OpenTelemetry.Instrumentation.MassTransit.Tests process", actualActivity.DisplayName);
                Assert.Equal(ActivityKind.Internal, actualActivity.Kind);

                Assert.Null(actualActivity.GetTagValue(TagName.SpanKind));
                Assert.Null(actualActivity.GetTagValue(TagName.PeerService));

                Assert.Null(actualActivity.GetTagValue(TagName.PeerAddress));
                Assert.Null(actualActivity.GetTagValue(TagName.PeerHost));
            }
        }

        [Theory]
        [InlineData(OperationName.Consumer.Consume)]
        [InlineData(OperationName.Consumer.Handle)]
        [InlineData(OperationName.Transport.Send)]
        [InlineData(OperationName.Transport.Receive)]
        public async Task MassTransitInstrumentationTestOptions(string operationName)
        {
            using Activity activity = new Activity("Parent");
            activity.SetParentId(
                ActivityTraceId.CreateRandom(),
                ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.Recorded);
            activity.Start();

            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            using (Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .AddMassTransitInstrumentation(o =>
                    o.TracedOperations = new HashSet<string>(new[] { operationName }))
                .Build())
            {
                var harness = new InMemoryTestHarness();
                var consumerHarness = harness.Consumer<TestConsumer>();
                var handlerHarness = harness.Handler<TestMessage>();
                await harness.Start();
                try
                {
                    await harness.InputQueueSendEndpoint.Send<TestMessage>(new
                    {
                        Text = "Hello, world!",
                    });

                    Assert.True(await harness.Consumed.SelectAsync<TestMessage>().Any());
                    Assert.True(await consumerHarness.Consumed.SelectAsync<TestMessage>().Any());
                    Assert.True(await handlerHarness.Consumed.SelectAsync().Any());
                }
                finally
                {
                    await harness.Stop();
                }
            }

            Assert.Equal(8, activityProcessor.Invocations.Count);

            var consumes = this.GetActivitiesFromInvocationsByOperationName(activityProcessor.Invocations, operationName);

            Assert.Single(consumes);
        }

        [Fact]
        public async Task ShouldMapMassTransitTagsWhenIntrumentationIsSuppressed()
        {
            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            using (Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .AddMassTransitInstrumentation()
                .Build())
            {
                var harness = new InMemoryTestHarness();
                var consumerHarness = harness.Consumer<TestConsumer>();
                var handlerHarness = harness.Handler<TestMessage>();
                using var scope = SuppressInstrumentationScope.Begin();
                await harness.Start();
                try
                {
                    await harness.InputQueueSendEndpoint.Send<TestMessage>(new { Text = "Hello, world!" });

                    Assert.True(await harness.Consumed.SelectAsync<TestMessage>().Any());
                    Assert.True(await consumerHarness.Consumed.SelectAsync<TestMessage>().Any());
                    Assert.True(await handlerHarness.Consumed.SelectAsync().Any());
                }
                finally
                {
                    await harness.Stop();
                }

                var expectedMessageContext = harness.Sent.Select<TestMessage>().FirstOrDefault()?.Context;
                Assert.NotNull(expectedMessageContext);
            }

            // Since instrumentation is suppressed, activiy is not emitted
            Assert.Equal(3, activityProcessor.Invocations.Count); // SetParentProvider + OnShutdown + Dispose

            // Processor.OnStart and Processor.OnEnd are not called
            Assert.DoesNotContain(activityProcessor.Invocations, invo => invo.Method.Name == nameof(activityProcessor.Object.OnStart));
            Assert.DoesNotContain(activityProcessor.Invocations, invo => invo.Method.Name == nameof(activityProcessor.Object.OnEnd));
        }

        [Theory]
        [InlineData(SamplingDecision.Drop, false)]
        [InlineData(SamplingDecision.RecordOnly, true)]
        [InlineData(SamplingDecision.RecordAndSample, true)]
        public async Task ShouldMapMassTransitTagsWhenIntrumentationWhenSampled(SamplingDecision samplingDecision, bool isActivityExpected)
        {
            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new TestSampler() { SamplingAction = (samplingParameters) => new SamplingResult(samplingDecision) })
                .AddProcessor(activityProcessor.Object)
                .AddMassTransitInstrumentation()
                .Build())
            {
                var harness = new InMemoryTestHarness();
                var consumerHarness = harness.Consumer<TestConsumer>();
                var handlerHarness = harness.Handler<TestMessage>();
                await harness.Start();
                try
                {
                    await harness.InputQueueSendEndpoint.Send<TestMessage>(new { Text = "Hello, world!" });

                    Assert.True(await harness.Consumed.SelectAsync<TestMessage>().Any());
                    Assert.True(await consumerHarness.Consumed.SelectAsync<TestMessage>().Any());
                    Assert.True(await handlerHarness.Consumed.SelectAsync().Any());
                }
                finally
                {
                    await harness.Stop();
                }

                var expectedMessageContext = harness.Sent.Select<TestMessage>().FirstOrDefault()?.Context;
                Assert.NotNull(expectedMessageContext);
            }

            Assert.Equal(isActivityExpected, activityProcessor.Invocations.Any(invo => invo.Method.Name == nameof(activityProcessor.Object.OnStart)));
            Assert.Equal(isActivityExpected, activityProcessor.Invocations.Any(invo => invo.Method.Name == nameof(activityProcessor.Object.OnEnd)));
        }

        private IEnumerable<Activity> GetActivitiesFromInvocationsByOperationName(IEnumerable<IInvocation> invocations, string operationName)
        {
            return
                invocations
                    .Where(i =>
                        i.Arguments.OfType<Activity>()
                            .Any(a => a.OperationName == operationName))
                    .Where(i => i.Method.Name == "OnEnd")
                    .Select(i => i.Arguments.OfType<Activity>().Single());
        }
    }
}
