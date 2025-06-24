// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Google.Api.Gax.Grpc;
using Google.Cloud.Monitoring.V3;
using Google.Cloud.Trace.V2;
using OpenTelemetry.Exporter.GoogleCloud.Implementation;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.GoogleCloud;

internal sealed class GoogleCloudMetricsExporter : BaseExporter<Metric>
{
    private static readonly string StackdriverExportVersion;
    private static readonly string OpenTelemetryExporterVersion;

    private readonly Google.Api.Gax.ResourceNames.ProjectName googleCloudProjectId;
    private readonly MetricService.MetricServiceClient metricServiceClient;

    public GoogleCloudMetricsExporter()
    {
        try
        {
            var assemblyPackageVersion = typeof(GoogleCloudTraceExporter).GetTypeInfo().Assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().First().InformationalVersion;
            StackdriverExportVersion = assemblyPackageVersion;
        }
        catch (Exception)
        {
            StackdriverExportVersion = $"{Constants.PackagVersionUndefined}";
        }

        try
        {
            OpenTelemetryExporterVersion = Assembly.GetCallingAssembly().GetName().Version!.ToString();
        }
        catch (Exception)
        {
            OpenTelemetryExporterVersion = $"{Constants.PackagVersionUndefined}";
        }
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleCloudMetricsExporter"/> class.
    /// </summary>
    /// <param name="projectId">Project ID to send telemetry to.</param>
    public GoogleCloudMetricsExporter(string projectId)
    {
        this.googleCloudProjectId = new Google.Api.Gax.ResourceNames.ProjectName(projectId);

        // Set header mutation for every outgoing API call to Stackdriver so the BE knows
        // which version of OC client is calling it as well as which version of the exporter
        var callSettings = CallSettings.FromHeaderMutation(StackdriverCallHeaderAppender);
        this.traceServiceSettings = new TraceServiceSettings { CallSettings = callSettings };
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleCloudTraceExporter"/> class.
    /// Only used internally for tests.
    /// </summary>
    /// <param name="projectId">Project ID to send telemetry to.</param>
    /// <param name="metricServiceClient">TraceServiceClient instance to use.</param>
    [ExcludeFromCodeCoverage]
    internal GoogleCloudMetricsExporter(string projectId, MetricService.MetricServiceClient metricServiceClient)
        : this(projectId)
    {
        this.metricServiceClient = metricServiceClient;
    }
    public override ExportResult Export(in Batch<Metric> batch)
    {
        try
        {
            foreach (var metric in batch)
            {
                metricServiceClient.CreateServiceTimeSeries()
                this.writer.Write(metric, this.ParentProvider?.GetResource(), this.writeApi);
            }

            return ExportResult.Success;
        }
        catch (Exception exception)
        {
            ExporterGoogleCloudEventSource.Log.FailedToExport(exception.Message);
            return ExportResult.Failure;
        }
    }
    /// <summary>
    /// Appends OpenTelemetry headers for every outgoing request to Stackdriver Backend.
    /// </summary>
    /// <param name="metadata">The metadata that is sent with every outgoing http request.</param>
    private static void StackdriverCallHeaderAppender(Metadata metadata)
    {
        metadata.Add("AGENT_LABEL_KEY", "g.co/agent");
        metadata.Add("AGENT_LABEL_VALUE_STRING", $"{OpenTelemetryExporterVersion}; googlecloud-exporter {StackdriverExportVersion}");
    }
    protected override void Dispose(bool disposing)
    {

        base.Dispose(disposing);
    }
}
