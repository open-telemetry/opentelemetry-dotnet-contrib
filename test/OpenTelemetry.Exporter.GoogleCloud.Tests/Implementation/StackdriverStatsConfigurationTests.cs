// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.GoogleCloud.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.GoogleCloud.Tests.Implementation;

public class GoogleCloudStatsConfigurationTests
{
    public GoogleCloudStatsConfigurationTests()
    {
        // Setting this for unit testing purposes, so we don't need credentials for real Google Cloud Account
        Environment.SetEnvironmentVariable("GOOGLE_PROJECT_ID", "test", EnvironmentVariableTarget.Process);
    }

    [Fact]
    public void StatsConfiguration_ByDefault_MetricNamePrefixEmpty()
    {
        Assert.NotNull(GoogleCloudStatsConfiguration.Default);
        Assert.Equal(GoogleCloudResourceUtils.GetProjectId(), GoogleCloudStatsConfiguration.Default.ProjectId);
        Assert.Equal(string.Empty, GoogleCloudStatsConfiguration.Default.MetricNamePrefix);
    }

    [Fact]
    public void StatsConfiguration_ByDefault_ProjectIdIsGoogleCloudProjectId()
    {
        Assert.NotNull(GoogleCloudStatsConfiguration.Default);
        Assert.Equal(GoogleCloudResourceUtils.GetProjectId(), GoogleCloudStatsConfiguration.Default.ProjectId);
    }

    [Fact]
    public void StatsConfiguration_ByDefault_ExportIntervalMinute()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), GoogleCloudStatsConfiguration.Default.ExportInterval);
    }

    [Fact]
    public void StatsConfiguration_ByDefault_MonitoredResourceIsGlobal()
    {
        Assert.NotNull(GoogleCloudStatsConfiguration.Default.MonitoredResource);

        Assert.Equal(Constants.Global, GoogleCloudStatsConfiguration.Default.MonitoredResource.Type);

        Assert.NotNull(GoogleCloudStatsConfiguration.Default.MonitoredResource.Labels);

        Assert.True(GoogleCloudStatsConfiguration.Default.MonitoredResource.Labels.ContainsKey("project_id"));
        Assert.True(GoogleCloudStatsConfiguration.Default.MonitoredResource.Labels.ContainsKey(Constants.ProjectIdLabelKey));
        Assert.Equal(
            GoogleCloudStatsConfiguration.Default.ProjectId,
            GoogleCloudStatsConfiguration.Default.MonitoredResource.Labels[Constants.ProjectIdLabelKey]);
    }
}
