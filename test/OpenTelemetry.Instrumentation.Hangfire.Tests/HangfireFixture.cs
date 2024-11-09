// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.Storage;

namespace OpenTelemetry.Instrumentation.Hangfire.Tests;

#pragma warning disable CA1515
public class HangfireFixture : IDisposable
#pragma warning restore CA1515
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
