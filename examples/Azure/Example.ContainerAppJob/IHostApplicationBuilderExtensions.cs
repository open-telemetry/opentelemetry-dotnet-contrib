// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources.Azure;
using OpenTelemetry.Trace;

namespace Example.ContainerAppJob;

public static class IHostApplicationBuilderExtensions
{
    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.AddAzureContainerAppsDetector();

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
             {
                 metrics.AddAspNetCoreInstrumentation()
                     .AddHttpClientInstrumentation()
                     .AddRuntimeInstrumentation();
             })
            .WithTracing(tracing =>
            {
                tracing.AddSource("Example.ContainerAppJob")
                    .AddAspNetCoreInstrumentation()

                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    // .AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static IHostApplicationBuilder AddAzureContainerAppsDetector(this IHostApplicationBuilder builder)
    {
        // Using AzureContainerAppsResourceDetector here requires adding the InternalsVisibleTo attribute in the OpenTelemetry.Resources.Azure package.
        builder.Services.AddSingleton<AzureContainerAppsResourceDetector>();

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(builder => builder
                .AddDetector(sp =>
                    sp.GetRequiredService<AzureContainerAppsResourceDetector>()));

        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            builder.Services.AddOpenTelemetry()
                            .UseAzureMonitor();
        }

        return builder;
    }
}
