// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using StackExchange.Redis;
using StackExchange.Redis.Profiling;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Benchmarks;

internal sealed class BenchmarkProfiledCommand : IProfiledCommand
{
    private readonly string command;
    private readonly BenchmarkRedisCommand? enumCommand;

    public BenchmarkProfiledCommand(
        DateTime commandCreated,
        EndPoint endPoint,
        int db,
        string command,
        bool commandFromEnum,
        TimeSpan creationToEnqueued,
        TimeSpan enqueuedToSending,
        TimeSpan sentToResponse,
        TimeSpan responseToCompletion)
    {
        this.CommandCreated = commandCreated;
        this.EndPoint = endPoint;
        this.Db = db;
        this.command = command;
        this.enumCommand = commandFromEnum ? Enum.Parse<BenchmarkRedisCommand>(command) : null;
        this.CreationToEnqueued = creationToEnqueued;
        this.EnqueuedToSending = enqueuedToSending;
        this.SentToResponse = sentToResponse;
        this.ResponseToCompletion = responseToCompletion;
    }

    public EndPoint EndPoint { get; }

    public int Db { get; }

    // StackExchange.Redis' ProfiledCommand.Command formats the RedisCommand enum on every access
    // (via Message.CommandString) rather than returning a stored string.
    public string Command => this.enumCommand?.ToString() ?? this.command;

    public CommandFlags Flags => CommandFlags.None;

    public DateTime CommandCreated { get; }

    public TimeSpan CreationToEnqueued { get; }

    public TimeSpan EnqueuedToSending { get; }

    public TimeSpan SentToResponse { get; }

    public TimeSpan ResponseToCompletion { get; }

    public TimeSpan ElapsedTime => this.CreationToEnqueued + this.EnqueuedToSending + this.SentToResponse + this.ResponseToCompletion;

    public IProfiledCommand? RetransmissionOf => null;

    public RetransmissionReasonType? RetransmissionReason => null;

    public static BenchmarkProfiledCommand Create(DateTime commandCreated, int index, bool commandFromEnum = false)
    {
        EndPoint endpoint = index % 2 == 0
            ? new IPEndPoint(IPAddress.Loopback, 6379)
            : new DnsEndPoint("localhost", 6379);

        return new BenchmarkProfiledCommand(
            commandCreated,
            endpoint,
            db: index % 16,
            command: index % 3 == 0 ? "GET" : "SET",
            commandFromEnum: commandFromEnum,
            creationToEnqueued: TimeSpan.FromTicks(15 + index),
            enqueuedToSending: TimeSpan.FromTicks(20 + index),
            sentToResponse: TimeSpan.FromTicks(35 + index),
            responseToCompletion: TimeSpan.FromTicks(10 + index));
    }
}
