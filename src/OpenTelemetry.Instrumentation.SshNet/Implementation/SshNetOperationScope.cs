// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Trace;
using Renci.SshNet;

namespace OpenTelemetry.Instrumentation.SshNet.Implementation;

/// <summary>
/// Tracks a single SSH/SFTP operation: owns the span (if sampled) and
/// records the duration metric when disposed.
/// </summary>
internal sealed class SshNetOperationScope : IDisposable
{
    private readonly SshNetInstrumentationOptions options;
    private readonly string protocol;
    private readonly string operation;
    private readonly string? serverAddress;
    private readonly int serverPort;
    private readonly long startTimestamp;
    private string? errorType;

    private SshNetOperationScope(
        SshNetInstrumentationOptions options,
        string protocol,
        string operation,
        ConnectionInfo? connectionInfo,
        string? path,
        string? targetPath)
    {
        this.options = options;
        this.protocol = protocol;
        this.operation = operation;
        this.serverAddress = connectionInfo?.Host;
        this.serverPort = connectionInfo?.Port ?? 0;
        this.startTimestamp = Stopwatch.GetTimestamp();

        var activity = SshNetTelemetry.ActivitySource.StartActivity($"{protocol} {operation}", ActivityKind.Client);
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(SemanticConventions.AttributeNetworkProtocolName, protocol);
            activity.SetTag(SshNetTelemetry.AttributeOperationName, operation);

            if (this.serverAddress != null)
            {
                activity.SetTag(SemanticConventions.AttributeServerAddress, this.serverAddress);
                activity.SetTag(SemanticConventions.AttributeServerPort, this.serverPort);
            }

            if (options.RecordFilePath)
            {
                if (path != null)
                {
                    activity.SetTag(SshNetTelemetry.AttributeFilePath, path);
                }

                if (targetPath != null)
                {
                    activity.SetTag(SshNetTelemetry.AttributeTargetPath, targetPath);
                }
            }
        }

        this.Activity = activity;
    }

    private Activity? Activity { get; }

    public static SshNetOperationScope Start(
        SshNetInstrumentationOptions options,
        string protocol,
        string operation,
        ConnectionInfo? connectionInfo,
        string? path = null,
        string? targetPath = null)
        => new(options, protocol, operation, connectionInfo, path, targetPath);

    public void SetTag(string key, object? value)
    {
        if (this.Activity is { IsAllDataRequested: true })
        {
            this.Activity.SetTag(key, value);
        }
    }

    public void SetException(Exception exception)
    {
        this.errorType = exception.GetType().FullName;

        var activity = this.Activity;
        if (activity == null)
        {
            return;
        }

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);

        if (activity.IsAllDataRequested)
        {
            activity.SetTag(SemanticConventions.AttributeErrorType, this.errorType);

            if (this.options.RecordException)
            {
                activity.AddException(exception);
            }
        }
    }

    public void Dispose()
    {
        this.Activity?.Dispose();

        if (SshNetTelemetry.OperationDuration.Enabled)
        {
            var tags = new TagList
            {
                { SemanticConventions.AttributeNetworkProtocolName, this.protocol },
                { SshNetTelemetry.AttributeOperationName, this.operation },
            };

            if (this.serverAddress != null)
            {
                tags.Add(SemanticConventions.AttributeServerAddress, this.serverAddress);
                tags.Add(SemanticConventions.AttributeServerPort, this.serverPort);
            }

            if (this.errorType != null)
            {
                tags.Add(SemanticConventions.AttributeErrorType, this.errorType);
            }

            var elapsed = (Stopwatch.GetTimestamp() - this.startTimestamp) / (double)Stopwatch.Frequency;
            SshNetTelemetry.OperationDuration.Record(elapsed, tags);
        }
    }
}
