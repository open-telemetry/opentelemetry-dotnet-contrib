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
using OpenTelemetry.Contrib.Instrumentation.MassTransit.Implementation;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Contrib.Instrumentation.MassTransit.Tests
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
                Assert.Equal("SEND /input_queue", actualActivity.DisplayName);
                Assert.Equal(ActivityKind.Client, actualActivity.Kind);
                Assert.Equal(expectedMessageContext.MessageId.ToString(), actualActivity.GetTagValue(TagName.MessageId).ToString());
                Assert.Equal(expectedMessageContext.ConversationId.ToString(), actualActivity.GetTagValue(TagName.ConversationId).ToString());
                Assert.Equal(expectedMessageContext.DestinationAddress.ToString(), actualActivity.GetTagValue(TagName.DestinationAddress).ToString());
                Assert.Equal(expectedMessageContext.SourceAddress.ToString(), actualActivity.GetTagValue(TagName.SourceAddress).ToString());
                Assert.Equal("/input_queue", actualActivity.GetTagValue(TagName.PeerAddress).ToString());
                Assert.Equal("localhost", actualActivity.GetTagValue(TagName.PeerHost).ToString());
                Assert.Equal("Send", actualActivity.GetTagValue(TagName.PeerService).ToString());
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
                Assert.Equal("RECV /input_queue", actualActivity.DisplayName);
                Assert.Equal(ActivityKind.Internal, actualActivity.Kind);
                Assert.Equal(expectedMessageContext.MessageId.ToString(), actualActivity.GetTagValue(TagName.MessageId).ToString());
                Assert.Equal(expectedMessageContext.ConversationId.ToString(), actualActivity.GetTagValue(TagName.ConversationId).ToString());
                Assert.Equal(expectedMessageContext.DestinationAddress.ToString(), actualActivity.GetTagValue(TagName.InputAddress).ToString());
                Assert.Equal(expectedMessageContext.DestinationAddress.ToString(), actualActivity.GetTagValue(TagName.DestinationAddress).ToString());
                Assert.Equal(expectedMessageContext.SourceAddress.ToString(), actualActivity.GetTagValue(TagName.SourceAddress).ToString());
                Assert.NotNull(actualActivity.GetTagValue("source-host-machine").ToString());
                Assert.NotNull(actualActivity.GetTagValue(TagName.MessageTypes).ToString());
                Assert.Equal("/input_queue", actualActivity.GetTagValue(TagName.PeerAddress).ToString());
                Assert.Equal("localhost", actualActivity.GetTagValue(TagName.PeerHost).ToString());
                Assert.Equal("Receive", actualActivity.GetTagValue(TagName.PeerService).ToString());
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
                Assert.Equal("CONSUME OpenTelemetry.Contrib.Instrumentation.MassTransit.Tests.TestConsumer", actualActivity.DisplayName);
                Assert.Equal(ActivityKind.Consumer, actualActivity.Kind);
                Assert.Equal("OpenTelemetry.Contrib.Instrumentation.MassTransit.Tests.TestConsumer", actualActivity.GetTagValue(TagName.ConsumerType).ToString());
                Assert.Equal("TestMessage/OpenTelemetry.Contrib.Instrumentation.MassTransit.Tests", actualActivity.GetTagValue(TagName.PeerAddress).ToString());
                Assert.NotNull(actualActivity.GetTagValue(TagName.PeerHost).ToString());
                Assert.Equal("Consumer", actualActivity.GetTagValue(TagName.PeerService).ToString());
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
                Assert.Equal("HANDLE TestMessage/OpenTelemetry.Contrib.Instrumentation.MassTransit.Tests", actualActivity.DisplayName);
                Assert.Equal(ActivityKind.Consumer, actualActivity.Kind);
                Assert.Equal("TestMessage/OpenTelemetry.Contrib.Instrumentation.MassTransit.Tests", actualActivity.GetTagValue(TagName.PeerAddress).ToString());
                Assert.NotNull(actualActivity.GetTagValue(TagName.PeerHost).ToString());
                Assert.Equal("Handler", actualActivity.GetTagValue(TagName.PeerService).ToString());
            }
        }

        [Fact]
        public async Task MassTransitInstrumentationTestOptions()
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
                    o.TracedOperations = new HashSet<string>(new[] { OperationName.Consumer.Consume }))
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

            Assert.Equal(4, activityProcessor.Invocations.Count);

            var consumes = this.GetActivitiesFromInvocationsByOperationName(activityProcessor.Invocations, OperationName.Consumer.Consume);

            Assert.Equal(2, consumes.Count());
        }

        private IEnumerable<Activity> GetActivitiesFromInvocationsByOperationName(IEnumerable<IInvocation> invocations, string operationName)
        {
            return
                invocations
                    .Where(i =>
                        i.Arguments.OfType<Activity>()
                            .Any(a => a.OperationName == operationName))
                    .Select(i => i.Arguments.OfType<Activity>().Single());
        }
    }
}
