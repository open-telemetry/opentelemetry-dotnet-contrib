// <copyright file="ServiceConnectDiagnosticListenerTests.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using OpenTelemetry.Instrumentation.ServiceConnect.Tests.Fixtures;
using ServiceConnect.Core;
using ServiceConnect.Interfaces;

namespace OpenTelemetry.Instrumentation.ServiceConnect.Tests;

public class ServiceConnectDiagnosticListenerTests
{
    private static readonly Guid CorrelationId = Guid.NewGuid();

    private readonly IBus sut;

    private readonly Mock<IConfiguration> configurationMock;
    private readonly Mock<IBusContainer> containerMock;
    private readonly Mock<IConsumer> consumerMock;

    private ConsumerEventHandler? myEventHandler;

    public ServiceConnectDiagnosticListenerTests()
    {
        this.containerMock = new Mock<IBusContainer>();
        this.consumerMock = new Mock<IConsumer>();

        this.configurationMock = new Mock<IConfiguration>();
        this.configurationMock.Setup(c => c.GetLogger()).Returns(new Mock<ILogger>().Object);
        this.configurationMock.Setup(c => c.GetContainer()).Returns(this.containerMock.Object);
        this.configurationMock.Setup(c => c.GetConsumer()).Returns(this.consumerMock.Object);
        this.configurationMock.Setup(c => c.GetProcessMessagePipeline(It.IsAny<BusState>())).Returns(new Mock<IProcessMessagePipeline>().Object);
        this.configurationMock.Setup(c => c.GetSendMessagePipeline()).Returns(new Mock<ISendMessagePipeline>().Object);
        this.configurationMock.Setup(c => c.AddBusToContainer).Returns(false);
        this.configurationMock.Setup(c => c.ScanForMesssageHandlers).Returns(false);
        this.configurationMock.Setup(c => c.AutoStartConsuming).Returns(false);
        this.configurationMock.Setup(c => c.EnableProcessManagerTimeouts).Returns(false);
        this.configurationMock.Setup(c => c.TransportSettings.QueueName).Returns("TestQueue");

        this.sut = new Bus(this.configurationMock.Object);
    }

    [Fact]
    public void ServiceConnectPublishCommandActivityStartStopTest()
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var tracer = this.GetTracer(activityProcessor.Object);
        MyMessage message = new(CorrelationId);

        this.sut.Publish(message);

        activityProcessor.Verify(x => x.OnStart(It.IsAny<Activity>()), Times.Once);
        activityProcessor.Verify(x => x.OnEnd(It.IsAny<Activity>()), Times.Once);
    }

    [Fact]
    public void ServiceConnectPublishCommandInstrumentedTest()
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var tracer = this.GetTracer(activityProcessor.Object);
        MyMessage message = new(CorrelationId);

        this.sut.Publish(message);

        Activity? activity = activityProcessor.Invocations[1].Arguments[0] as Activity;
        Assert.Equal("OpenTelemetry.Instrumentation.ServiceConnect.Bus.Publish", activity?.OperationName);
        Assert.Equal(ActivityKind.Producer, activity?.Kind);
        Assert.Equal("anonymous publish", activity?.DisplayName);
        Assert.Equal("rabbitmq", activity?.Tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeMessagingSystem).Value);
        Assert.Equal("amqp", activity?.Tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeMessagingProtocol).Value);
        Assert.Equal("publish", activity?.Tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeMessagingOperation).Value);
        Assert.Equal("true", activity?.Tags.FirstOrDefault(x => x.Key == "messaging.destination.anonymous").Value);
        Assert.Equal(CorrelationId.ToString(), activity?.Tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeMessagingConversationId).Value);
    }

    [Fact]
    public void ServiceConnectPublishCommandWithHeadersInstrumentedTest()
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var tracer = this.GetTracer(activityProcessor.Object);
        MyMessage message = new(CorrelationId);
        var messageId = Guid.NewGuid().ToString();
        Dictionary<string, string> headers = new()
        {
                { "MessageId", messageId },
        };

        this.sut.Publish(message, headers);

        Activity? activity = activityProcessor.Invocations[1].Arguments[0] as Activity;
        Assert.Equal(messageId, activity?.Tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeMessagingMessageId).Value);
    }

    [Fact]
    public void ServiceConnectPublishCommandWithRoutingKeyInstrumentedTest()
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var tracer = this.GetTracer(activityProcessor.Object);
        MyMessage message = new(CorrelationId);
        var routingKey = "TestQueue";

        this.sut.Publish(message, routingKey);

        Activity? activity = activityProcessor.Invocations[1].Arguments[0] as Activity;
        Assert.Equal("TestQueue publish", activity?.DisplayName);
        Assert.Equal("TestQueue", activity?.Tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeMessagingDestination).Value);
    }

    [Fact]
    public void ServiceConnectPublishCommandWithMessageEnrichmentInstrumentedTest()
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var tracer = this.GetTracer(activityProcessor.Object, options =>
        {
            options.EnrichWithMessage = (activity, message) =>
            {
                _ = activity.SetTag("MessageVersion", (message as MyMessage)?.Version.ToString());
            };
        });
        MyMessage message = new(CorrelationId);

        this.sut.Publish(message);

        Activity? activity = activityProcessor.Invocations[1].Arguments[0] as Activity;
        Assert.Equal("1", activity?.Tags.FirstOrDefault(x => x.Key == "MessageVersion").Value);
    }

    [Fact]
    public async Task ServiceConnectConsumeCommandActivityStartStopTest()
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var tracer = this.GetTracer(activityProcessor.Object);
        var headers = new Dictionary<string, object>
        {
            { "MessageType", Encoding.ASCII.GetBytes("Send") },
        };
        this.SetupConsumer();
        byte[] message = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new MyMessage(CorrelationId)));

        await this.myEventHandler!(message, typeof(MyMessage).AssemblyQualifiedName, headers);

        activityProcessor.Verify(x => x.OnStart(It.IsAny<Activity>()), Times.Once);
        activityProcessor.Verify(x => x.OnEnd(It.IsAny<Activity>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("TestQueue")]
    public async Task ServiceConnectConsumeCommandInstrumentedTest(string destinationAddress)
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var tracer = this.GetTracer(activityProcessor.Object);
        var messageId = Guid.NewGuid().ToString();
        var headers = new Dictionary<string, object>
        {
            { "MessageType", Encoding.ASCII.GetBytes("Send") },
            { "MessageId", Encoding.ASCII.GetBytes(messageId) },
        };
        if (destinationAddress is not null)
        {
            headers["DestinationAddress"] = Encoding.ASCII.GetBytes(destinationAddress);
        }

        this.SetupConsumer();
        byte[] message = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new MyMessage(CorrelationId)));

        await this.myEventHandler!(message, typeof(MyMessage).AssemblyQualifiedName, headers);

        Activity? activity = activityProcessor.Invocations[1].Arguments[0] as Activity;
        Assert.Equal("OpenTelemetry.Instrumentation.ServiceConnect.Bus.Consume", activity?.OperationName);
        Assert.Equal(ActivityKind.Consumer, activity?.Kind);
        if (destinationAddress is null)
        {
            Assert.Equal("anonymous receive", activity?.DisplayName);
            Assert.Equal("true", activity?.Tags.FirstOrDefault(x => x.Key == "messaging.destination.anonymous").Value);
        }
        else
        {
            Assert.Equal($"{destinationAddress} receive", activity?.DisplayName);
            Assert.Equal(destinationAddress, activity?.Tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeMessagingDestination).Value);
        }

        Assert.Equal("rabbitmq", activity?.Tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeMessagingSystem).Value);
        Assert.Equal("amqp", activity?.Tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeMessagingProtocol).Value);
        Assert.Equal("receive", activity?.Tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeMessagingOperation).Value);
        Assert.Equal(messageId, activity?.Tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeMessagingMessageId).Value);
    }

    [Fact]
    public async Task ServiceConnectConsumeCommandWithMessageBytesEnrichmentInstrumentedTest()
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var tracer = this.GetTracer(activityProcessor.Object, options =>
        {
            options.EnrichWithMessageBytes = (activity, message) =>
            {
                _ = activity.SetTag("MessageVersion", JsonConvert.DeserializeObject<MyMessage>(Encoding.UTF8.GetString(message))?.Version.ToString());
            };
        });

        var headers = new Dictionary<string, object>
        {
            { "MessageType", Encoding.ASCII.GetBytes("Send") },
        };

        this.SetupConsumer();
        byte[] message = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new MyMessage(CorrelationId)));

        await this.myEventHandler!(message, typeof(MyMessage).AssemblyQualifiedName, headers);

        Activity? activity = activityProcessor.Invocations[1].Arguments[0] as Activity;
        Assert.Equal("1", activity?.Tags.FirstOrDefault(x => x.Key == "MessageVersion").Value);
    }

    [Fact]
    public void ServiceConnectSendCommandActivityStartStopTest()
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var tracer = this.GetTracer(activityProcessor.Object);
        MyMessage message = new(CorrelationId);

        this.sut.Send(message, headers: null);

        activityProcessor.Verify(x => x.OnStart(It.IsAny<Activity>()), Times.Once);
        activityProcessor.Verify(x => x.OnEnd(It.IsAny<Activity>()), Times.Once);
    }

    [Fact]
    public void ServiceConnectSendCommandWithEndPointActivityStartStopTest()
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var tracer = this.GetTracer(activityProcessor.Object);
        MyMessage message = new(CorrelationId);
        var endPoint = "Endpoint.Test";

        this.sut.Send(endPoint, message, headers: null);

        activityProcessor.Verify(x => x.OnStart(It.IsAny<Activity>()), Times.Once);
        activityProcessor.Verify(x => x.OnEnd(It.IsAny<Activity>()), Times.Once);
    }

    [Fact]
    public void ServiceConnectSendCommandWithEndPointsActivityStartStopTest()
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var tracer = this.GetTracer(activityProcessor.Object);
        MyMessage message = new(CorrelationId);
        List<string> endPoints = new() { "Endpoint.Test" };

        this.sut.Send(endPoints, message, headers: null);

        activityProcessor.Verify(x => x.OnStart(It.IsAny<Activity>()), Times.Once);
        activityProcessor.Verify(x => x.OnEnd(It.IsAny<Activity>()), Times.Once);
    }

    [Fact]
    public void ServiceConnectSendCommandInstrumentedTest()
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var tracer = this.GetTracer(activityProcessor.Object);
        MyMessage message = new(CorrelationId);

        this.sut.Send(message, headers: null);

        Activity? activity = activityProcessor.Invocations[1].Arguments[0] as Activity;
        Assert.Equal("OpenTelemetry.Instrumentation.ServiceConnect.Bus.Send", activity?.OperationName);
        Assert.Equal(ActivityKind.Producer, activity?.Kind);
        Assert.Equal("anonymous publish", activity?.DisplayName);
        Assert.Equal("rabbitmq", activity?.Tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeMessagingSystem).Value);
        Assert.Equal("amqp", activity?.Tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeMessagingProtocol).Value);
        Assert.Equal("publish", activity?.Tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeMessagingOperation).Value);
        Assert.Equal("true", activity?.Tags.FirstOrDefault(x => x.Key == "messaging.destination.anonymous").Value);
        Assert.Equal(CorrelationId.ToString(), activity?.Tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeMessagingConversationId).Value);
    }

    [Fact]
    public void ServiceConnectSendCommandWithEndPointInstrumentedTest()
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var tracer = this.GetTracer(activityProcessor.Object);
        MyMessage message = new(CorrelationId);
        string endPoint = "Test.Service";

        this.sut.Send(endPoint, message, headers: null);

        Activity? activity = activityProcessor.Invocations[1].Arguments[0] as Activity;
        Assert.Equal($"{endPoint} publish", activity?.DisplayName);
        Assert.Equal(endPoint, activity?.Tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeMessagingDestination).Value);
    }

    [Fact]
    public void ServiceConnectSendCommandWithEndPointsInstrumentedTest()
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var tracer = this.GetTracer(activityProcessor.Object);
        MyMessage message = new(CorrelationId);
        List<string> endPoints = new() { "Test.Service1", "Test.Service2" };

        this.sut.Send(endPoints, message, headers: null);

        Activity? activity = activityProcessor.Invocations[1].Arguments[0] as Activity;
        Assert.Equal("[Test.Service1,Test.Service2] publish", activity?.DisplayName);
        Assert.Equal("[Test.Service1,Test.Service2]", activity?.Tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeMessagingDestination).Value);
    }

    private TracerProvider? GetTracer(BaseProcessor<Activity> activityProcessor, Action<ServiceConnectInstrumentationOptions>? options = null)
    {
        return Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor)
            .AddServiceConnectInstrumentation(options)
            .Build();
    }

    private void SetupConsumer()
    {
        List<HandlerReference> handlerReferences = new()
        {
                new HandlerReference
                {
                    HandlerType = typeof(MyHandler),
                    MessageType = typeof(MyMessage),
                },
        };
        this.containerMock.Setup(x => x.GetHandlerTypes()).Returns(handlerReferences);
        this.consumerMock.Setup(x => x.StartConsuming(It.IsAny<string>(), It.IsAny<IList<string>>(), It.Is<ConsumerEventHandler>(y => this.AssignEventHandler(y)), It.IsAny<IConfiguration>()));

        this.sut.StartConsuming();
    }

    private bool AssignEventHandler(ConsumerEventHandler eventHandler)
    {
        this.myEventHandler = eventHandler;
        return true;
    }
}
