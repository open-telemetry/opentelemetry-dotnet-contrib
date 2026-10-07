// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Benchmarks;

/// <summary>
/// Stand-in for StackExchange.Redis' internal <c>RedisCommand</c> enum.
/// </summary>
internal enum BenchmarkRedisCommand
{
    NONE,
    APPEND,
    AUTH,
    DEL,
    EXISTS,
    EXPIRE,
    GET,
    HGET,
    HSET,
    INCR,
    SET,
}
