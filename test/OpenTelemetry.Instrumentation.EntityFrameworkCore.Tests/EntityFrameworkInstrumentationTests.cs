// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Tests;

public class EntityFrameworkInstrumentationTests
{
    [Fact]
    public void ServerAddressWithoutProtocolPrefix()
    {
        var activity = new Activity("TestActivity");
        activity.Start();

        var connection = new
        {
            Host = "my.domain.example",
            DataSource = "tcp:my.domain.example",
            Port = "5432",
        };

        var hostFetcher = new PropertyFetcher<string>("Host");
        var dataSourceFetcher = new PropertyFetcher<string>("DataSource");
        var portFetcher = new PropertyFetcher<string>("Port");

        var host = hostFetcher.Fetch(connection);
        if (!string.IsNullOrEmpty(host))
        {
            activity.AddTag("server.address", host);
        }
        else
        {
            var dataSource = dataSourceFetcher.Fetch(connection);
            if (!string.IsNullOrEmpty(dataSource))
            {
                activity.AddTag("server.address", dataSource);
            }
        }

        var port = portFetcher.Fetch(connection);
        if (!string.IsNullOrEmpty(port))
        {
            activity.AddTag("server.port", port);
        }

        activity.Stop();

        Assert.Equal("my.domain.example", activity.Tags.FirstOrDefault(t => t.Key == "server.address").Value);
        Assert.Equal("5432", activity.Tags.FirstOrDefault(t => t.Key == "server.port").Value);
    }
}
