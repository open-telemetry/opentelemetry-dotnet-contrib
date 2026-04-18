// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using OpenTelemetry.Exporter.Instana.Implementation;
using OpenTelemetry.Exporter.Instana.Implementation.Processors;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Instana;

internal sealed class InstanaExporter : BaseExporter<Activity>
{
    private readonly SpanSender sender;
    private readonly IActivityProcessor activityProcessor;
    private readonly string? processId;

    private int wasShutdown;

    public InstanaExporter(InstanaExporterOptions options, IActivityProcessor? activityProcessor = null)
    {
        this.sender = new SpanSender(options);
        this.activityProcessor = activityProcessor ?? new DefaultActivityProcessor
        {
            NextProcessor = new TagsActivityProcessor
            {
                NextProcessor = new EventsActivityProcessor { NextProcessor = new ErrorActivityProcessor() },
            },
        };

        if (this.Helper.IsWindows())
        {
#if NET
            this.processId = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
#else
            using var process = Process.GetCurrentProcess();
            this.processId = process.Id.ToString(CultureInfo.InvariantCulture);
#endif
        }
    }

    internal IInstanaExporterHelper Helper { get; set; } = new InstanaExporterHelper();

    public override ExportResult Export(in Batch<Activity> batch)
    {
        if (this.wasShutdown is 1)
        {
            return ExportResult.Failure;
        }

        var from = new From();

        if (this.processId != null)
        {
            from.E = this.processId;
        }

        var serviceName = this.ExtractServiceName(ref from);

        foreach (var activity in batch)
        {
            if (activity == null)
            {
                continue;
            }

            var span = this.ParseActivity(activity, serviceName, from);

            _ = this.sender.Enqueue(span);
        }

        return ExportResult.Success;
    }

    protected override void Dispose(bool disposing)
    {
        this.sender?.Dispose();
        base.Dispose(disposing);
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        var wasShutdown = Interlocked.CompareExchange(ref this.wasShutdown, 1, 0);
        return wasShutdown == 0;
    }

    private string ExtractServiceName(ref From from)
    {
        var serviceName = string.Empty;
        var serviceId = string.Empty;
        var processId = string.Empty;
        var hostId = string.Empty;
        var resource = this.Helper.GetParentProviderResource(this);

        if (resource != Resource.Empty && resource.Attributes.Any())
        {
            var comparison = StringComparison.OrdinalIgnoreCase;

            foreach (var resourceAttribute in resource.Attributes)
            {
                if (string.Equals(resourceAttribute.Key, "service.name", comparison)
                    && resourceAttribute.Value is string name
                    && !string.IsNullOrEmpty(name))
                {
                    serviceName = name;
                }

                if (from.IsEmpty())
                {
                    if (string.Equals(resourceAttribute.Key, "service.instance.id", comparison))
                    {
                        serviceId = resourceAttribute.Value.ToString();
                    }
                    else if (string.Equals(resourceAttribute.Key, "process.pid", comparison))
                    {
                        processId = resourceAttribute.Value.ToString();
                    }
                    else if (string.Equals(resourceAttribute.Key, "host.id", comparison))
                    {
                        hostId = resourceAttribute.Value.ToString();
                    }
                }
            }
        }

        if (from.IsEmpty())
        {
            if (!string.IsNullOrEmpty(processId))
            {
                from.E = processId;
            }
            else if (!string.IsNullOrEmpty(serviceId))
            {
                from.E = serviceId;
            }

            if (!string.IsNullOrEmpty(hostId))
            {
                from.H = hostId;
            }
        }

        return serviceName;
    }

    private InstanaSpan ParseActivity(Activity activity, string? serviceName = null, From? from = null)
    {
        var instanaSpan = InstanaSpanFactory.CreateSpan();

        this.activityProcessor.Process(activity, instanaSpan);

        if (serviceName != null && !string.IsNullOrEmpty(serviceName) && instanaSpan.Data.data != null)
        {
            instanaSpan.Data.data[InstanaExporterConstants.SERVICE_FIELD] = serviceName;
        }

        instanaSpan.Data.data?[InstanaExporterConstants.OPERATION_FIELD] = activity.DisplayName;

        if (activity.TraceStateString != null && !string.IsNullOrEmpty(activity.TraceStateString) && instanaSpan.Data.data != null)
        {
            instanaSpan.Data.data[InstanaExporterConstants.TRACE_STATE_FIELD] = activity.TraceStateString;
        }

        if (from != null)
        {
            instanaSpan.F = from;
        }

        return instanaSpan;
    }
}
