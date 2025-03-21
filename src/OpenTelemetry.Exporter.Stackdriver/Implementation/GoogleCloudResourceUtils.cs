// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Api;

namespace OpenTelemetry.Exporter.Stackdriver.Implementation;

/// <summary>
/// Utility methods for working with Google Cloud Resources.
/// </summary>
public static class GoogleCloudResourceUtils
{
    /// <summary>
    /// Detects Google Cloud ProjectId based on the environment on which the code runs.
    /// Supports GCE/GKE/GAE and projectId tied to service account
    /// In case the code runs in a different environment,
    /// the method returns null.
    /// </summary>
    /// <returns>Google Cloud Project ID.</returns>
    public static string? GetProjectId()
    {
        // Try to detect projectId from the environment where the code is running
        var instance = Google.Api.Gax.Platform.Instance();
        var projectId = instance?.ProjectId;
        if (!string.IsNullOrEmpty(projectId))
        {
            return projectId;
        }

        // Try to detect projectId from service account credential if it exists
        var serviceAccountFilePath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
        if (!string.IsNullOrEmpty(serviceAccountFilePath) && File.Exists(serviceAccountFilePath))
        {
            using var stream = new FileStream(serviceAccountFilePath, FileMode.Open, FileAccess.Read);
            var credential = Google.Apis.Auth.OAuth2.ServiceAccountCredential.FromServiceAccountData(stream);
            return credential.ProjectId;
        }

        projectId = Environment.GetEnvironmentVariable("GOOGLE_PROJECT_ID");
        return projectId;
    }

    /// <summary>
    /// Determining the resource to which the metrics belong.
    /// </summary>
    /// <param name="projectId">The project id.</param>
    /// <returns>Stackdriver Monitored Resource.</returns>
    public static MonitoredResource GetDefaultResource(string? projectId)
    {
        var resource = new MonitoredResource();
        resource.Type = Constants.Global;
        resource.Labels.Add(Constants.ProjectIdLabelKey, projectId);

        // TODO - zeltser - setting monitored resource labels for detected resource
        // along with all the other metadata

        return resource;
    }
}
