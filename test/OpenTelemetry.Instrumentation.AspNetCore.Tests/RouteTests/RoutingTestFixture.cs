// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Builder;
using RouteTests.TestApplication;
using Xunit;

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

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
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
        await HttpClient.GetAsync(new Uri(url));
    }

    internal void AddActivityTestResult(ActivityRoutingTestResult result)
    {
        this.activityTestResults.Add(result);
    }

    internal void AddMetricsTestResult(MetricRoutingTestResult result)
    {
        this.metricsTestResults.Add(result);
    }

    private void GenerateReadme()
    {
        var sb = new StringBuilder();
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

        string routeTestsPath =
            typeof(TestApplicationFactory).Assembly
            .GetCustomAttributes()
            .OfType<AssemblyMetadataAttribute>()
            .FirstOrDefault((p) => p.Key is "RouteTestsPath")?.Value ?? ".";

        var readmeFileName = $"README.net{Environment.Version.Major}.0.md";
        File.WriteAllText(Path.Combine(routeTestsPath, readmeFileName), sb.ToString());
    }

    private void AppendTestResults(StringBuilder sb, IReadOnlyCollection<RoutingTestResult> testResults)
    {
        for (var i = 0; i < testResults.Count; ++i)
        {
            var result = testResults.ElementAt(i);
            var emoji = result.TestCase.CurrentHttpRoute == null ? ":green_heart:" : ":broken_heart:";
            sb.AppendLine($"| {emoji} | {result.TestCase.TestApplicationScenario} | [{result.TestCase.Name}]({GenerateLinkFragment(result.TestCase.TestApplicationScenario, result.TestCase.Name)}) |");
        }

        for (var i = 0; i < testResults.Count; ++i)
        {
            var result = testResults.ElementAt(i);
            sb.AppendLine();
            sb.AppendLine($"## {result.TestCase.TestApplicationScenario}: {result.TestCase.Name}");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(result.ToString());
            sb.AppendLine("```");
        }

        // Generates a link fragment that should comply with markdownlint rule MD051
        // https://github.com/DavidAnson/markdownlint/blob/main/doc/md051.md
        static string GenerateLinkFragment(TestApplicationScenario scenario, string name)
        {
            var chars = name.ToCharArray()
                .Where(c => (!char.IsPunctuation(c) && c != '`') || c == '-')
                .Select(c => c switch
                {
                    '-' or ' ' => '-',
                    _ => char.ToLower(c, CultureInfo.InvariantCulture),
                })
                .ToArray();

            return $"#{scenario.ToString().ToLower(CultureInfo.CurrentCulture)}-{new string(chars)}";
        }
    }
}
