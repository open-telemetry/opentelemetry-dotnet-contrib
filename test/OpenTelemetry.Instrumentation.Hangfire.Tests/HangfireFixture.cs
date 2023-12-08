// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.Storage;

namespace OpenTelemetry.Instrumentation.Hangfire.Tests;

public class HangfireFixture : IDisposable
{
    public HangfireFixture()
    {
        GlobalConfiguration.Configuration
            .UseMemoryStorage();
        this.Server = new BackgroundJobServer();
        this.MonitoringApi = JobStorage.Current.GetMonitoringApi();
    }

    public BackgroundJobServer Server { get; }

    public IMonitoringApi MonitoringApi { get; }

    public void Dispose()
    {
        this.Server.Dispose();
    }
}
