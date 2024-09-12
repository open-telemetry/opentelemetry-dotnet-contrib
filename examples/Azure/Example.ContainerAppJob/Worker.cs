// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using OpenTelemetry.Trace;

namespace Example.ContainerAppJob;

public class Worker(IHostApplicationLifetime hostApplicationLifetime, IHttpClientFactory httpClientFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (var exampleActivity = OpenTelemetryDiagnostics.ActivitySource.StartActivity("Example activity"))
        {
            try
            {
                await Task.Delay(1000, stoppingToken);
                exampleActivity?.AddEvent(new System.Diagnostics.ActivityEvent("Example event"));

                var client = httpClientFactory.CreateClient();
                var testUri = new Uri("https://www.microsoft.com/");
                var result = await client.GetAsync(testUri, stoppingToken);

                // Loop over environment variables and add them as tags
                foreach (DictionaryEntry envVar in (IDictionary)Environment.GetEnvironmentVariables())
                {
                    if (envVar.Key is null)
                    {
                        continue;
                    }

                    exampleActivity?.SetTag(envVar.Key.ToString()!, envVar.Value?.ToString());
                }

                await Task.Delay(1000, stoppingToken);

                // Simulate an exception.
                throw new Exception("Example exception");
            }
            catch (Exception ex)
            {
                exampleActivity?.RecordException(ex);
            }
        }

        // When completed, the entire app host will stop.
        hostApplicationLifetime.StopApplication();
    }
}
