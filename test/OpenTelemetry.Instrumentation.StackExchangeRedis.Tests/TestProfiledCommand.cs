// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using StackExchange.Redis;
using StackExchange.Redis.Profiling;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Tests;

internal class TestProfiledCommand(DateTime commandCreated) : IProfiledCommand
{
    private readonly DateTime commandCreated = commandCreated;
    private readonly CommandFlags flags = CommandFlags.None;
    private readonly EndPoint endPoint = new IPEndPoint(0, 0);
    private readonly TimeSpan enqueuedToSending;

    public TestProfiledCommand(DateTime commandCreated, CommandFlags flags)
        : this(commandCreated)
    {
        this.flags = flags;
    }

    public TestProfiledCommand(DateTime commandCreated, EndPoint endpoint)
        : this(commandCreated)
    {
        this.endPoint = endpoint;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestProfiledCommand"/> class
    /// that simulates a connection failure (command was enqueued but never sent).
    /// When <paramref name="connectionFailed"/> is <see langword="true"/>, the
    /// <see cref="EnqueuedToSending"/> property returns a negative value, mirroring
    /// how StackExchange.Redis exposes commands that were aborted due to a connection
    /// timeout or connection failure.
    /// </summary>
    public TestProfiledCommand(DateTime commandCreated, bool connectionFailed)
        : this(commandCreated)
    {
        if (connectionFailed)
        {
            this.enqueuedToSending = TimeSpan.FromSeconds(-1);
        }
    }

    public EndPoint EndPoint => this.endPoint;

    public int Db => 0;

    public string Command => "SET";

    public CommandFlags Flags => this.flags;

    public DateTime CommandCreated => this.commandCreated;

    public TimeSpan CreationToEnqueued => default;

    public TimeSpan EnqueuedToSending => this.enqueuedToSending;

    public TimeSpan SentToResponse => default;

    public TimeSpan ResponseToCompletion => default;

    public TimeSpan ElapsedTime => default;

    public IProfiledCommand RetransmissionOf => throw new NotImplementedException();

    public RetransmissionReasonType? RetransmissionReason => throw new NotImplementedException();
}
