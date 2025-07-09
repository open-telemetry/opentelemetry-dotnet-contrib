// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace Examples.SimpleConsoleExporter;

public class Program
{
    // Usage:
    //   --logger otel-simpleconsole   (default, OpenTelemetry SimpleConsoleExporter)
    //   --logger otel-console        (OpenTelemetry ConsoleExporter)
    //   --logger default              (.NET Console logger, or use 'dotnet')
    //   --logger dotnet-json         (.NET Console logger, JSON formatter)
    //   --logger dotnet-systemd       (.NET Console logger, Syslog formatter)

    public static async Task Main(string[] args)
    {
        // Add ActivitySource listener to enable activity creation
        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var loggerType = ParseLoggerType(args);
        var timestampFormat = ParseTimestampFormat(args);

        var builder = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton(TimeProvider.System);
                services.AddHostedService<Worker>();
            });

        switch (loggerType)
        {
            case "DOTNET":
            case "DEFAULT":
                builder.ConfigureLogging(logging =>
                {
                    if (!string.IsNullOrEmpty(timestampFormat))
                    {
                        logging.ClearProviders();
                        logging.AddSimpleConsole(options =>
                        {
                            options.TimestampFormat = timestampFormat;
                        });
                    }
                    logging.SetMinimumLevel(LogLevel.Trace);
                });
                break;
            case "DOTNET-JSON":
                builder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddJsonConsole(options =>
                    {
                        if (!string.IsNullOrEmpty(timestampFormat))
                        {
                            options.TimestampFormat = timestampFormat;
                        }
                    });
                    logging.SetMinimumLevel(LogLevel.Trace);
                });
                break;
            case "DOTNET-SYSTEMD":
                builder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddSystemdConsole();
                    logging.SetMinimumLevel(LogLevel.Trace);
                });
                break;
            case "OTEL-CONSOLE":
                builder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddOpenTelemetry(options =>
                    {
                        options.IncludeFormattedMessage = true;
                        options.AddConsoleExporter();
                    });
                    logging.SetMinimumLevel(LogLevel.Trace);
                });
                break;
            case "OTEL-SIMPLECONSOLE":
            default:
                builder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddOpenTelemetry(options =>
                    {
                        options.IncludeFormattedMessage = true;
                        options.AddSimpleConsoleExporter(exporterOptions =>
                        {
                            if (!string.IsNullOrEmpty(timestampFormat))
                            {
                                exporterOptions.TimestampFormat = timestampFormat;
                            }
                        });
                    });
                    logging.SetMinimumLevel(LogLevel.Trace);
                });
                break;
        }

        var host = builder.Build();
        await host.RunAsync();
    }

    private static string ParseLoggerType(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--logger", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var value = args[i + 1].ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(value))
                {
                    return "OTEL-SIMPLECONSOLE";
                }

                return value;
            }
        }

        return "OTEL-SIMPLECONSOLE";
    }

    private static string? ParseTimestampFormat(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--timestamp", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
