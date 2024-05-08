// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Builder;
using RouteTests.TestApplication;
using Xunit;

namespace RouteTests;

public class RoutingTestFixture : IAsyncLifetime
{
    private static readonly HttpClient HttpClient = new();
    private readonly Dictionary<TestApplicationScenario, WebApplication> apps = new();
    private readonly RouteInfoDiagnosticObserver diagnostics = new();
    private readonly List<RoutingTestResult> testResults = new();

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

    public void AddTestResult(RoutingTestResult result)
    {
        this.testResults.Add(result);
    }

    private void GenerateReadme()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Test results for ASP.NET Core {Environment.Version.Major}");
        sb.AppendLine();
        sb.AppendLine("| http.route | App | Test Name |");
        sb.AppendLine("| - | - | - |");

        for (var i = 0; i < this.testResults.Count; ++i)
        {
            var result = this.testResults[i];
            var emoji = result.TestCase.CurrentHttpRoute == null ? ":green_heart:" : ":broken_heart:";
            sb.AppendLine($"| {emoji} | {result.TestCase.TestApplicationScenario} | [{result.TestCase.Name}]({GenerateLinkFragment(result.TestCase.TestApplicationScenario, result.TestCase.Name)}) |");
        }

        for (var i = 0; i < this.testResults.Count; ++i)
        {
            var result = this.testResults[i];
            sb.AppendLine();
            sb.AppendLine($"## {result.TestCase.TestApplicationScenario}: {result.TestCase.Name}");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(result.ToString());
            sb.AppendLine("```");
        }

        var readmeFileName = $"README.net{Environment.Version.Major}.0.md";
        File.WriteAllText(Path.Combine("..", "..", "..", "RouteTests", readmeFileName), sb.ToString());

        // Generates a link fragment that should comply with markdownlint rule MD051
        // https://github.com/DavidAnson/markdownlint/blob/main/doc/md051.md
        static string GenerateLinkFragment(TestApplicationScenario scenario, string name)
        {
            var chars = name.ToCharArray()
                .Where(c => (!char.IsPunctuation(c) && c != '`') || c == '-')
                .Select(c => c switch
                {
                    '-' => '-',
                    ' ' => '-',
                    _ => char.ToLower(c, CultureInfo.InvariantCulture),
                })
                .ToArray();

            return $"#{scenario.ToString().ToLower(CultureInfo.CurrentCulture)}-{new string(chars)}";
        }
    }
}
