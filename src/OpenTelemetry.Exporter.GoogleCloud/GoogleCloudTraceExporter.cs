// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Google.Api.Gax.Grpc;
using Google.Cloud.Trace.V2;
using Grpc.Core;
using OpenTelemetry.Exporter.GoogleCloud.Implementation;

namespace OpenTelemetry.Exporter.GoogleCloud;

/// <summary>
/// Exports a group of spans to Stackdriver.
/// </summary>
[Obsolete("This exporter is deprecated and might be removed in a future version. Please consider using directly OTPL directly: https://cloud.google.com/stackdriver/docs/reference/telemetry/overview")]
public class GoogleCloudTraceExporter : BaseExporter<Activity>
{
    private static readonly string GoogleCloudTraceExportVersion;
    private static readonly string OpenTelemetryExporterVersion;

    private readonly Google.Api.Gax.ResourceNames.ProjectName googleCloudProjectId;
    private readonly TraceServiceSettings traceServiceSettings;
    private readonly TraceServiceClient? traceServiceClient;

#pragma warning disable CA1810 // Initialize reference type static fields inline
    static GoogleCloudTraceExporter()
#pragma warning restore CA1810 // Initialize reference type static fields inline
    {
        try
        {
            var assemblyPackageVersion = typeof(GoogleCloudTraceExporter).GetTypeInfo().Assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().First().InformationalVersion;
            GoogleCloudTraceExportVersion = assemblyPackageVersion;
        }
        catch (Exception)
        {
            GoogleCloudTraceExportVersion = $"{Constants.PackagVersionUndefined}";
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
    /// Initializes a new instance of the <see cref="GoogleCloudTraceExporter"/> class.
    /// </summary>
    /// <param name="projectId">Project ID to send telemetry to.</param>
    public GoogleCloudTraceExporter(string projectId)
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
    /// <param name="traceServiceClient">TraceServiceClient instance to use.</param>
    [ExcludeFromCodeCoverage]
    internal GoogleCloudTraceExporter(string projectId, TraceServiceClient traceServiceClient)
        : this(projectId)
    {
        this.traceServiceClient = traceServiceClient;
    }

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Activity> batch)
    {
        var traceWriter = this.traceServiceClient ?? new TraceServiceClientBuilder
        {
            Settings = this.traceServiceSettings,
        }.Build();

        var batchSpansRequest = new BatchWriteSpansRequest
        {
            ProjectName = this.googleCloudProjectId,
        };

        var resource = this.ParentProvider?.GetResource();
        foreach (var activity in batch)
        {
            // It should never happen that the time has no correct kind, only if OpenTelemetry is used incorrectly.
            if (activity.StartTimeUtc.Kind == DateTimeKind.Utc)
            {
                var span = activity.ToSpan(this.googleCloudProjectId.ProjectId);
                if (resource != null)
                {
                    span.AnnotateWith(resource);
                }

                batchSpansRequest.Spans.Add(span);
            }
        }

        // avoid cancelling here: this is no return point: if we reached this point
        // and cancellation is requested, it's better if we try to finish sending spans rather than drop it
        try
        {
            traceWriter.BatchWriteSpans(batchSpansRequest);
        }
        catch (Exception ex)
        {
            ExporterGoogleCloudEventSource.Log.ExportMethodException(ex);

            return ExportResult.Failure;
        }

        return ExportResult.Success;
    }

    /// <summary>
    /// Appends OpenTelemetry headers for every outgoing request to Stackdriver Backend.
    /// </summary>
    /// <param name="metadata">The metadata that is sent with every outgoing http request.</param>
    private static void StackdriverCallHeaderAppender(Metadata metadata)
    {
        metadata.Add("AGENT_LABEL_KEY", "g.co/agent");
        metadata.Add("AGENT_LABEL_VALUE_STRING", $"{OpenTelemetryExporterVersion}; googlecloud-exporter {GoogleCloudTraceExportVersion}");
    }
}
