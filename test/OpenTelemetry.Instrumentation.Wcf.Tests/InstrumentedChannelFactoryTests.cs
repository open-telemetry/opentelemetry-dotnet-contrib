// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.ServiceModel;
using System.ServiceModel.Channels;
using OpenTelemetry.Instrumentation.Wcf.Implementation;
using Xunit;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

public class InstrumentedChannelFactoryTests
{
    [Fact]
    public void CreateChannel_ForRequestChannel_DoesNotAdvertiseSessionWhenInnerChannelDoesNotSupportIt()
    {
        var innerChannel = new RecordingRequestChannel();
        var innerFactory = new RecordingChannelFactory<IRequestChannel>(innerChannel);
        var factory = (IChannelFactory<IRequestChannel>)new InstrumentedChannelFactory<IRequestChannel>(innerFactory, new CustomBinding());

        var channel = factory.CreateChannel(new EndpointAddress("net.tcp://localhost/Service"));

        Assert.IsNotAssignableFrom<IRequestSessionChannel>(channel);
    }

    [Fact]
    public void CreateChannel_ForDuplexChannel_DoesNotAdvertiseSessionWhenInnerChannelDoesNotSupportIt()
    {
        var innerChannel = new RecordingDuplexChannel();
        var innerFactory = new RecordingChannelFactory<IDuplexChannel>(innerChannel);
        var factory = (IChannelFactory<IDuplexChannel>)new InstrumentedChannelFactory<IDuplexChannel>(innerFactory, new CustomBinding());

        var channel = factory.CreateChannel(new EndpointAddress("net.tcp://localhost/Service"));

        Assert.IsNotAssignableFrom<IDuplexSessionChannel>(channel);
    }

    private sealed class FakeAsyncResult : IAsyncResult
    {
        public FakeAsyncResult(object? asyncState)
        {
            this.AsyncState = asyncState;
        }

        public object? AsyncState { get; }

        public WaitHandle AsyncWaitHandle { get; } = new ManualResetEvent(initialState: true);

        public bool CompletedSynchronously => true;

        public bool IsCompleted => true;
    }

    private sealed class RecordingChannelFactory<TChannel> : IChannelFactory<TChannel>
        where TChannel : IChannel
    {
        private readonly TChannel channel;

        public RecordingChannelFactory(TChannel channel)
        {
            this.channel = channel;
        }

        event EventHandler ICommunicationObject.Closed
        {
            add
            {
            }

            remove
            {
            }
        }

        event EventHandler ICommunicationObject.Closing
        {
            add
            {
            }

            remove
            {
            }
        }

        event EventHandler ICommunicationObject.Faulted
        {
            add
            {
            }

            remove
            {
            }
        }

        event EventHandler ICommunicationObject.Opened
        {
            add
            {
            }

            remove
            {
            }
        }

        event EventHandler ICommunicationObject.Opening
        {
            add
            {
            }

            remove
            {
            }
        }

        public CommunicationState State => CommunicationState.Opened;

        public TChannel CreateChannel(EndpointAddress to)
            => this.channel;

        public TChannel CreateChannel(EndpointAddress to, Uri via)
            => this.channel;

        public T? GetProperty<T>()
            where T : class
            => default;

        public void Abort()
        {
        }

        public IAsyncResult BeginClose(AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public IAsyncResult BeginClose(TimeSpan timeout, AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public IAsyncResult BeginOpen(AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public IAsyncResult BeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public void Close()
        {
        }

        public void Close(TimeSpan timeout)
        {
        }

        public void EndClose(IAsyncResult result)
        {
        }

        public void EndOpen(IAsyncResult result)
        {
        }

        public void Open()
        {
        }

        public void Open(TimeSpan timeout)
        {
        }
    }

    private abstract class RecordingChannel : IChannel
    {
        event EventHandler ICommunicationObject.Closed
        {
            add
            {
            }

            remove
            {
            }
        }

        event EventHandler ICommunicationObject.Closing
        {
            add
            {
            }

            remove
            {
            }
        }

        event EventHandler ICommunicationObject.Faulted
        {
            add
            {
            }

            remove
            {
            }
        }

        event EventHandler ICommunicationObject.Opened
        {
            add
            {
            }

            remove
            {
            }
        }

        event EventHandler ICommunicationObject.Opening
        {
            add
            {
            }

            remove
            {
            }
        }

        public CommunicationState State => CommunicationState.Opened;

        public T? GetProperty<T>()
            where T : class
            => default;

        public void Abort()
        {
        }

        public IAsyncResult BeginClose(AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public IAsyncResult BeginClose(TimeSpan timeout, AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public IAsyncResult BeginOpen(AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public IAsyncResult BeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public void Close()
        {
        }

        public void Close(TimeSpan timeout)
        {
        }

        public void EndClose(IAsyncResult result)
        {
        }

        public void EndOpen(IAsyncResult result)
        {
        }

        public void Open()
        {
        }

        public void Open(TimeSpan timeout)
        {
        }
    }

    private sealed class RecordingRequestChannel : RecordingChannel, IRequestChannel
    {
        public EndpointAddress RemoteAddress { get; } = new("net.tcp://localhost/Service");

        public Uri Via { get; } = new("net.tcp://localhost/Service");

        public Message Request(Message message)
            => Message.CreateMessage(MessageVersion.Soap11, "urn:reply");

        public Message Request(Message message, TimeSpan timeout)
            => Message.CreateMessage(MessageVersion.Soap11, "urn:reply");

        public IAsyncResult BeginRequest(Message message, AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public IAsyncResult BeginRequest(Message message, TimeSpan timeout, AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public Message EndRequest(IAsyncResult result)
            => Message.CreateMessage(MessageVersion.Soap11, "urn:reply");
    }

    private sealed class RecordingDuplexChannel : RecordingChannel, IDuplexChannel
    {
        public EndpointAddress LocalAddress { get; } = new("net.tcp://localhost/Local");

        public EndpointAddress RemoteAddress { get; } = new("net.tcp://localhost/Service");

        public Uri Via { get; } = new("net.tcp://localhost/Service");

        public void Send(Message message)
        {
        }

        public void Send(Message message, TimeSpan timeout)
        {
        }

        public IAsyncResult BeginSend(Message message, AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public IAsyncResult BeginSend(Message message, TimeSpan timeout, AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public void EndSend(IAsyncResult result)
        {
        }

        public Message Receive()
            => Message.CreateMessage(MessageVersion.Soap11, "urn:reply");

        public Message Receive(TimeSpan timeout)
            => Message.CreateMessage(MessageVersion.Soap11, "urn:reply");

        public IAsyncResult BeginReceive(AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public IAsyncResult BeginReceive(TimeSpan timeout, AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public Message EndReceive(IAsyncResult result)
            => Message.CreateMessage(MessageVersion.Soap11, "urn:reply");

        public bool TryReceive(TimeSpan timeout, out Message message)
        {
            message = Message.CreateMessage(MessageVersion.Soap11, "urn:reply");
            return true;
        }

        public IAsyncResult BeginTryReceive(TimeSpan timeout, AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public bool EndTryReceive(IAsyncResult result, out Message message)
        {
            message = Message.CreateMessage(MessageVersion.Soap11, "urn:reply");
            return true;
        }

        public bool WaitForMessage(TimeSpan timeout)
            => true;

        public IAsyncResult BeginWaitForMessage(TimeSpan timeout, AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public bool EndWaitForMessage(IAsyncResult result)
            => true;
    }
}
