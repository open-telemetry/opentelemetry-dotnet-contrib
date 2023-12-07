// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using OpenTelemetry.Exporter.Stackdriver.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.Stackdriver.Tests;

public class StackdriverStatsConfigurationTests
{
    public StackdriverStatsConfigurationTests()
    {
        // Setting this for unit testing purposes, so we don't need credentials for real Google Cloud Account
        Environment.SetEnvironmentVariable("GOOGLE_PROJECT_ID", "test", EnvironmentVariableTarget.Process);
    }

    [Fact]
    public void StatsConfiguration_ByDefault_MetricNamePrefixEmpty()
    {
        Assert.NotNull(StackdriverStatsConfiguration.Default);
        Assert.Equal(GoogleCloudResourceUtils.GetProjectId(), StackdriverStatsConfiguration.Default.ProjectId);
        Assert.Equal(string.Empty, StackdriverStatsConfiguration.Default.MetricNamePrefix);
    }

    [Fact]
    public void StatsConfiguration_ByDefault_ProjectIdIsGoogleCloudProjectId()
    {
        Assert.NotNull(StackdriverStatsConfiguration.Default);
        Assert.Equal(GoogleCloudResourceUtils.GetProjectId(), StackdriverStatsConfiguration.Default.ProjectId);
    }

    [Fact]
    public void StatsConfiguration_ByDefault_ExportIntervalMinute()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), StackdriverStatsConfiguration.Default.ExportInterval);
    }

    [Fact]
    public void StatsConfiguration_ByDefault_MonitoredResourceIsGlobal()
    {
        Assert.NotNull(StackdriverStatsConfiguration.Default.MonitoredResource);

        Assert.Equal(Constants.Global, StackdriverStatsConfiguration.Default.MonitoredResource.Type);

        Assert.NotNull(StackdriverStatsConfiguration.Default.MonitoredResource.Labels);

        Assert.True(StackdriverStatsConfiguration.Default.MonitoredResource.Labels.ContainsKey("project_id"));
        Assert.True(StackdriverStatsConfiguration.Default.MonitoredResource.Labels.ContainsKey(Constants.ProjectIdLabelKey));
        Assert.Equal(
            StackdriverStatsConfiguration.Default.ProjectId,
            StackdriverStatsConfiguration.Default.MonitoredResource.Labels[Constants.ProjectIdLabelKey]);
    }
}
