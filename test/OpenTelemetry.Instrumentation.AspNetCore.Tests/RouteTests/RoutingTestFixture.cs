// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using RouteTests.TestApplication;

namespace RouteTests;

public class RoutingTestFixture : IAsyncLifetime
{
    private static readonly HttpClient HttpClient = new();
    private readonly Dictionary<TestApplicationScenario, WebApplication> apps = [];
    private readonly RouteInfoDiagnosticObserver diagnostics = new();
    private readonly List<ActivityRoutingTestResult> activityTestResults = [];
    private readonly List<MetricRoutingTestResult> metricsTestResults = [];

    public RoutingTestFixture()
    {
        foreach (var scenario in Enum.GetValues<TestApplicationScenario>())
        {
            var app = TestApplicationFactory.CreateApplication(scenario);
            if (app != null)
            {
                this.apps.Add(scenario, app);
            }
        }

        foreach (var app in this.apps)
        {
            app.Value.RunAsync();
        }
    }

    public ValueTask InitializeAsync()
        => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var app in this.apps)
        {
            await app.Value.DisposeAsync();
        }

        HttpClient.Dispose();
        this.diagnostics.Dispose();

        this.GenerateReadme();
    }

    public async Task MakeRequest(TestApplicationScenario scenario, string path)
    {
        var app = this.apps[scenario];
        var baseUrl = app.Urls.First();
        var url = $"{baseUrl}{path}";

        // Hosting records http.server.request.duration after the response has been sent, so the
        // client can observe the response before the measurement is recorded. Wait for it so that a
        // test does not collect metrics while the measurement is still being recorded. The listener
        // is started after any MeterProvider created by the test so that it is notified after the SDK.
        var meterFactory = app.Services.GetRequiredService<IMeterFactory>();
        var durationRecorded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Scope == meterFactory &&
                instrument.Meter.Name == "Microsoft.AspNetCore.Hosting" &&
                instrument.Name == "http.server.request.duration")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, _, _, _) => durationRecorded.TrySetResult());
        listener.Start();

        using var response = await HttpClient.GetAsync(new Uri(url));

        await durationRecorded.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    internal void AddActivityTestResult(ActivityRoutingTestResult result)
        => this.activityTestResults.Add(result);

    internal void AddMetricsTestResult(MetricRoutingTestResult result)
        => this.metricsTestResults.Add(result);

    private void GenerateReadme()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!-- markdownlint-disable MD022 MD024 MD031 MD033 -->");
        sb.AppendLine();
        sb.AppendLine($"# Test results for ASP.NET Core {Environment.Version.Major}");
        sb.AppendLine();

        // Append tracing header
        sb.AppendLine("## Tracing");
        sb.AppendLine();
        sb.AppendLine("| http.route | App | Test Name |");
        sb.AppendLine("| - | - | - |");

        this.AppendTestResults(sb, this.activityTestResults);

        // Append metrics header
        sb.AppendLine();
        sb.AppendLine("## Metrics");
        sb.AppendLine();
        sb.AppendLine("| http.route | App | Test Name |");
        sb.AppendLine("| - | - | - |");

        this.AppendTestResults(sb, this.metricsTestResults);

        sb.AppendLine();
        sb.AppendLine("## Tracing tests details");
        sb.AppendLine();

        this.AppendJsonResults(sb, this.activityTestResults);

        sb.AppendLine();
        sb.AppendLine("## Metrics tests details");
        sb.AppendLine();

        this.AppendJsonResults(sb, this.metricsTestResults);

        var routeTestsPath =
            typeof(TestApplicationFactory).Assembly
            .GetCustomAttributes()
            .OfType<AssemblyMetadataAttribute>()
            .FirstOrDefault((p) => p.Key is "RouteTestsPath")?.Value ?? ".";

        var readmeFileName = $"README.net{Environment.Version.Major}.0.md";
        File.WriteAllText(Path.Combine(routeTestsPath, readmeFileName), sb.ToString());
    }

    private void AppendTestResults(StringBuilder sb, IReadOnlyCollection<RoutingTestResult> testResults)
    {
        foreach (var result in testResults)
        {
            var emoji = result.TestCase.CurrentHttpRoute == null ? ":green_heart:" : ":broken_heart:";
            sb.AppendLine($"| {emoji} | {result.TestCase.TestApplicationScenario} | [{result.TestCase.Name}](#{result.DetailsAnchor}) |");
        }
    }

    private void AppendJsonResults(StringBuilder sb, IReadOnlyCollection<RoutingTestResult> testResults)
    {
        foreach (var result in testResults)
        {
            sb.AppendLine($"<a name=\"{result.DetailsAnchor}\"></a>");
            sb.AppendLine($"## {result.TestCase.TestApplicationScenario}: {result.TestCase.Name}");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(result.ToString());
            sb.AppendLine("```");
        }
    }
}
