// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using StackExchange.Redis;
using StackExchange.Redis.Profiling;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Tests;

internal class TestProfiledCommand(
    DateTime commandCreated,
    CommandFlags flags = CommandFlags.None,
    EndPoint? endpoint = null,
    TimeSpan elapsedTime = default) : IProfiledCommand
{
    public EndPoint EndPoint { get; } = endpoint ?? new IPEndPoint(0, 0);

    public int Db => 0;

    public string Command => "SET";

    public CommandFlags Flags { get; } = flags;

    public DateTime CommandCreated { get; } = commandCreated;

    public TimeSpan CreationToEnqueued => default;

    public TimeSpan EnqueuedToSending => default;

    public TimeSpan SentToResponse => default;

    public TimeSpan ResponseToCompletion => default;

    public TimeSpan ElapsedTime { get; } = elapsedTime;

    public IProfiledCommand RetransmissionOf => throw new NotImplementedException();

    public RetransmissionReasonType? RetransmissionReason => throw new NotImplementedException();
}
