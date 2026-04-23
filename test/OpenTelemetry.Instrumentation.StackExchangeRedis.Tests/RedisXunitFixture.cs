// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Tests;

public class RedisXunitFixture : RedisFixture, IAsyncLifetime
{
    Task IAsyncLifetime.DisposeAsync() => this.DisposeAsync().AsTask();

    public Task InitializeAsync() => this.StartAsync();
}
