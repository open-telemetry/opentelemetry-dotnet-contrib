// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Api;
using Google.Apis.Auth.OAuth2;

namespace OpenTelemetry.Exporter.GoogleCloud.Implementation;

/// <summary>
/// Configuration for exporting stats into GoogleCloudMonitoring .
/// </summary>
public class GoogleCloudStatsConfiguration
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets default Stats Configuration for GoogleCloudMonitoring .
    /// </summary>
    public static GoogleCloudStatsConfiguration Default
    {
        get
        {
            var defaultConfig = new GoogleCloudStatsConfiguration
            {
                ExportInterval = DefaultInterval,
                ProjectId = GoogleCloudResourceUtils.GetProjectId(),
                MetricNamePrefix = string.Empty,
            };

            defaultConfig.MonitoredResource = GoogleCloudResourceUtils.GetDefaultResource(defaultConfig.ProjectId);
            return defaultConfig;
        }
    }

    /// <summary>
    /// Gets or sets frequency of the export operation.
    /// </summary>
    public TimeSpan ExportInterval { get; set; }

    /// <summary>
    /// Gets or sets the prefix to append to every OpenTelemetry metric name in GoogleCloudMonitoring .
    /// </summary>
    public string? MetricNamePrefix { get; set; }

    /// <summary>
    /// Gets or sets google Cloud Project Id.
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets credential used to authenticate against Google GoogleCloudMonitoring  Monitoring APIs.
    /// </summary>
    public GoogleCredential? GoogleCredential { get; set; }

    /// <summary>
    /// Gets or sets monitored Resource associated with metrics collection.
    /// By default, the exporter detects the environment where the export is happening,
    /// such as GKE/AWS/GCE. If the exporter is running on a different environment,
    /// monitored resource will be identified as "general".
    /// </summary>
    public MonitoredResource? MonitoredResource { get; set; }
}
