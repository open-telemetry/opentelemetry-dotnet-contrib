// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Text.Json;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Instana.Tests;

public class TracerProviderBuilderExtensionsTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task AddInstanaExporter_WithEnvironmentVariables_Minimal()
    {
        // Arrange
        var agentKey = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var server = TestHttpServer.RunServer(
            (context) => AssertResponse(context, agentKey, tcs),
            out var host,
            out var port);

        var endpoint = new UriBuilder(Uri.UriSchemeHttp, host, port).Uri;

        using (EnvironmentVariableScope.Create(
            [
                new("INSTANA_AGENT_KEY", agentKey),
                new("INSTANA_ENDPOINT_URL", endpoint.ToString()),
            ]))
        {
            await this.AddInstanaExporterExportsTraces(tcs, (builder) =>
            {
                builder.AddInstanaExporter();
            });
        }
    }

    [Fact]
    public async Task AddInstanaExporter_WithEnvironmentVariables_All()
    {
        // Arrange
        var agentKey = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var server = TestHttpServer.RunServer(
            (context) => AssertResponse(context, agentKey, tcs),
            out var host,
            out var port);

        var endpoint = new UriBuilder(Uri.UriSchemeHttp, host, port).Uri;

        using (EnvironmentVariableScope.Create(
            [
                new("INSTANA_AGENT_KEY", agentKey),
                new("INSTANA_ENDPOINT_URL", endpoint.ToString()),
                new("INSTANA_ENDPOINT_PROXY", endpoint.ToString()),
                new("INSTANA_TIMEOUT", "60000"),
            ]))
        {
            await this.AddInstanaExporterExportsTraces(tcs, (builder) =>
            {
                builder.AddInstanaExporter();
            });
        }
    }

    [Fact]
    public async Task AddInstanaExporter_WithOptions()
    {
        // Arrange
        var agentKey = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var server = TestHttpServer.RunServer(
            (context) => AssertResponse(context, agentKey, tcs),
            out var host,
            out var port);

        var endpoint = new UriBuilder(Uri.UriSchemeHttp, host, port).Uri;

        await this.AddInstanaExporterExportsTraces(tcs, (builder) =>
        {
            builder.AddInstanaExporter((options) =>
            {
                options.AgentKey = agentKey;
                options.EndpointUri = endpoint;

                options.HttpClientFactory = () =>
                {
                    var handler = new HttpClientHandler()
                    {
#if NET
                        CheckCertificateRevocationList = true,
                        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
#else
                        ServerCertificateCustomValidationCallback = static (_, _, _, _) => true,
#endif
                    };
                    return new HttpClient(handler, disposeHandler: true);
                };
            });
        });
    }

    private static void AssertResponse(
        HttpListenerContext context,
        string agentKey,
        TaskCompletionSource<string> completionSource)
    {
        try
        {
            Assert.Equal("POST", context.Request.HttpMethod);
            Assert.Equal(agentKey, context.Request.Headers["X-INSTANA-KEY"]);

            using var reader = new StreamReader(context.Request.InputStream);
            completionSource.SetResult(reader.ReadToEnd());
        }
        catch (Exception ex)
        {
            completionSource.SetException(ex);
        }
    }

    private async Task AddInstanaExporterExportsTraces(
        TaskCompletionSource<string> completionSource,
        Action<TracerProviderBuilder> configure)
    {
        // Arrange
        using var activitySource = new ActivitySource(Guid.NewGuid().ToString());

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySource.Name)
            .SetSampler(new AlwaysOnSampler());

        configure(builder);

        using var provider = builder.Build();

        // Act
        using (var activity = activitySource.StartActivity(Guid.NewGuid().ToString()))
        {
            activity?.AddTag("service.name", Guid.NewGuid().ToString());
        }

        // Assert
#if NET
        await completionSource.Task.WaitAsync(Timeout);
#else
        using var cts = new CancellationTokenSource(Timeout);
        var completed = await Task.WhenAny(completionSource.Task, Task.Delay(Timeout, cts.Token));
        Assert.Same(completionSource.Task, completed);
#endif

        var actual = await completionSource.Task;

        using var document = JsonDocument.Parse(actual);
        Assert.NotNull(document);

        var exportedSpans = document.RootElement.GetProperty("spans");
        Assert.NotEmpty(exportedSpans.EnumerateArray());
    }
}
