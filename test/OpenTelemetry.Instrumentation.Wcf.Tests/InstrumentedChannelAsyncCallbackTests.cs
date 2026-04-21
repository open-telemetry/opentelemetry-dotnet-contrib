// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.ServiceModel;
using System.ServiceModel.Channels;
using OpenTelemetry.Instrumentation.Wcf.Implementation;
using Xunit;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

public class InstrumentedChannelAsyncCallbackTests
{
    [Fact]
    public void BeginRequest_AllowsNullAsyncCallback()
    {
        var inner = new RecordingRequestChannel();
        var channel = (IRequestChannel)new InstrumentedRequestChannel(inner);
        var state = new object();

        var asyncResult = channel.BeginRequest(
            Message.CreateMessage(MessageVersion.Soap11, "urn:test"),
            callback: null,
            state);

        Assert.NotNull(asyncResult);
        Assert.NotNull(inner.LastBeginRequestArgs);
        Assert.Null(inner.LastBeginRequestArgs![1]);
        Assert.Same(state, inner.LastBeginRequestArgs[2]);
    }

    [Fact]
    public void BeginSend_AllowsNullAsyncCallback()
    {
        var inner = new RecordingDuplexChannel();
        var channel = new InstrumentedDuplexChannel(inner, TimeSpan.FromSeconds(1));
        var state = new object();

        var asyncResult = channel.BeginSend(
            Message.CreateMessage(MessageVersion.Soap11, "urn:test"),
            callback: null,
            state);

        Assert.NotNull(asyncResult);
        Assert.NotNull(inner.LastBeginSendArgs);
        Assert.Null(inner.LastBeginSendArgs![1]);
        Assert.Same(state, inner.LastBeginSendArgs[2]);
    }

    [Fact]
    public void BeginReceive_AllowsNullAsyncCallback()
    {
        var inner = new RecordingDuplexChannel();
        var channel = new InstrumentedDuplexChannel(inner, TimeSpan.FromSeconds(1));
        var state = new object();

        var asyncResult = channel.BeginReceive(callback: null, state);

        Assert.NotNull(asyncResult);
        Assert.NotNull(inner.LastBeginReceiveArgs);
        Assert.Null(inner.LastBeginReceiveArgs![0]);
        Assert.Same(state, inner.LastBeginReceiveArgs[1]);
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

        public virtual T? GetProperty<T>()
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
        public object?[]? LastBeginRequestArgs { get; private set; }

        public EndpointAddress RemoteAddress { get; } = new("net.tcp://localhost/Service");

        public Uri Via { get; } = new("net.tcp://localhost/Service");

        public Message Request(Message message)
            => Message.CreateMessage(MessageVersion.Soap11, "urn:reply");

        public Message Request(Message message, TimeSpan timeout)
            => Message.CreateMessage(MessageVersion.Soap11, "urn:reply");

        public IAsyncResult BeginRequest(Message message, AsyncCallback callback, object state)
        {
            this.LastBeginRequestArgs = [message, callback, state];
            return new FakeAsyncResult(state);
        }

        public IAsyncResult BeginRequest(Message message, TimeSpan timeout, AsyncCallback callback, object state)
        {
            this.LastBeginRequestArgs = [message, timeout, callback, state];
            return new FakeAsyncResult(state);
        }

        public Message EndRequest(IAsyncResult result)
            => Message.CreateMessage(MessageVersion.Soap11, "urn:reply");
    }

    private sealed class RecordingDuplexChannel : RecordingChannel, IDuplexChannel
    {
        public object?[]? LastBeginReceiveArgs { get; private set; }

        public object?[]? LastBeginSendArgs { get; private set; }

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
        {
            this.LastBeginSendArgs = [message, callback, state];
            return new FakeAsyncResult(state);
        }

        public IAsyncResult BeginSend(Message message, TimeSpan timeout, AsyncCallback callback, object state)
        {
            this.LastBeginSendArgs = [message, timeout, callback, state];
            return new FakeAsyncResult(state);
        }

        public void EndSend(IAsyncResult result)
        {
        }

        public Message Receive()
            => Message.CreateMessage(MessageVersion.Soap11, "urn:reply");

        public Message Receive(TimeSpan timeout)
            => Message.CreateMessage(MessageVersion.Soap11, "urn:reply");

        public IAsyncResult BeginReceive(AsyncCallback callback, object state)
        {
            this.LastBeginReceiveArgs = [callback, state];
            return new FakeAsyncResult(state);
        }

        public IAsyncResult BeginReceive(TimeSpan timeout, AsyncCallback callback, object state)
        {
            this.LastBeginReceiveArgs = [timeout, callback, state];
            return new FakeAsyncResult(state);
        }

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
