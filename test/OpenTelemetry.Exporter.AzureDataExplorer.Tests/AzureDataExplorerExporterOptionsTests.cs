// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Azure.Identity;

namespace OpenTelemetry.Exporter.AzureDataExplorer.Tests;

public class AzureDataExplorerExporterOptionsTests
{
    private static readonly Uri ClusterUri = new("https://mycluster.kusto.windows.net");

    [Fact]
    public void Defaults_MatchCollectorExporter()
    {
        var options = new AzureDataExplorerExporterOptions();

        Assert.Equal("oteldb", options.DatabaseName);
        Assert.Equal("OTELMetrics", options.MetricsTableName);
        Assert.Equal("OTELLogs", options.LogsTableName);
        Assert.Equal("OTELTraces", options.TracesTableName);
        Assert.Equal(AzureDataExplorerIngestionType.Queued, options.IngestionType);
    }

    [Fact]
    public void HttpClientFactory_DefaultsToSharedClientAndRejectsNull()
    {
        var options = new AzureDataExplorerExporterOptions();

        Assert.Same(KustoRestIngestor.DefaultHttpClient, options.HttpClientFactory());
        Assert.Same(options.HttpClientFactory(), new AzureDataExplorerExporterOptions().HttpClientFactory());
        Assert.Throws<ArgumentNullException>(() => options.HttpClientFactory = null!);
    }

    [Fact]
    public void Validate_RequiresClusterUri()
    {
        var options = new AzureDataExplorerExporterOptions { UseAzureAuth = true };
        Assert.Throws<InvalidOperationException>(options.Validate);

        options.ClusterUri = new Uri("/relative", UriKind.Relative);
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Theory]
    [InlineData(null, null, null, null, false, false)]
    [InlineData("app", "key", null, null, false, false)]
    [InlineData("app", "key", "tenant", "system", false, false)]
    [InlineData(null, null, null, "system", true, false)]
    [InlineData(null, null, null, null, true, true)]
    [InlineData("app", "key", "tenant", null, false, true)]
    [InlineData(null, null, null, "not-a-guid", false, false)]
    [InlineData("app", "key", "tenant", null, false, false, true)]
    [InlineData(null, null, null, "system", false, true)]
    [InlineData(null, null, null, " SYSTEM ", false, true)]
    [InlineData(null, null, null, "8a3f0c3d-6fd8-4a52-a6b1-3a5cf53ac1d4", false, true)]
    [InlineData(null, null, null, null, false, true, true)]
    public void Validate_RequiresExactlyOneValidAuthenticationMethod(
        string? applicationId,
        string? applicationKey,
        string? tenantId,
        string? managedIdentityId,
        bool useAzureAuth,
        bool valid,
        bool useTokenCredential = false)
    {
        var options = new AzureDataExplorerExporterOptions
        {
            ClusterUri = ClusterUri,
            ApplicationId = applicationId,
            ApplicationKey = applicationKey,
            TenantId = tenantId,
            ManagedIdentityId = managedIdentityId,
            UseAzureAuth = useAzureAuth,
            TokenCredential = useTokenCredential ? new DefaultAzureCredential() : null,
        };

        if (valid)
        {
            options.Validate();
        }
        else
        {
            Assert.Throws<InvalidOperationException>(options.Validate);
        }
    }

    [Fact]
    public void Validate_RejectsInvalidIngestionTypeAndDatabase()
    {
        var options = new AzureDataExplorerExporterOptions
        {
            ClusterUri = ClusterUri,
            UseAzureAuth = true,
            IngestionType = (AzureDataExplorerIngestionType)42,
        };
        Assert.Throws<InvalidOperationException>(options.Validate);

        options.IngestionType = AzureDataExplorerIngestionType.Managed;
        options.DatabaseName = " ";
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void ExportTimeout_RejectsNonPositiveValues()
    {
        var options = new AzureDataExplorerExporterOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.ExportTimeout = TimeSpan.Zero);
        options.ExportTimeout = Timeout.InfiniteTimeSpan;
        Assert.Equal(Timeout.InfiniteTimeSpan, options.ExportTimeout);
    }

    [Fact]
    public void Exporter_RequiresTableName()
    {
        var ingestor = new TestIngestor();
        var options = TestIngestor.CreateOptions(ingestor);
        options.TracesTableName = string.Empty;

        Assert.Throws<InvalidOperationException>(() => new AzureDataExplorerTraceExporter(options));
    }

    [Fact]
    public void Credential_ApplicationKey()
    {
        var authorityHost = new Uri("https://login.microsoftonline.us/");
        var credential = KustoRestIngestor.CreateCredential(
            new AzureDataExplorerExporterOptions
            {
                ClusterUri = ClusterUri,
                ApplicationId = "8a3f0c3d-6fd8-4a52-a6b1-3a5cf53ac1d4",
                ApplicationKey = "key",
                TenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47",
            },
            authorityHost);

        Assert.IsType<ClientSecretCredential>(credential);
        Assert.StartsWith("Kusto.OpenTelemetry.Exporter.AzureDataExplorer:", KustoRestIngestor.GetApplicationName(isManagedIdentity: false), StringComparison.Ordinal);
        Assert.EndsWith("|isManagedIdentity:{false}", KustoRestIngestor.GetApplicationName(isManagedIdentity: false), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("system")]
    [InlineData("8a3f0c3d-6fd8-4a52-a6b1-3a5cf53ac1d4")]
    public void Credential_ManagedIdentity(string managedIdentityId)
    {
        var credential = KustoRestIngestor.CreateCredential(
            new AzureDataExplorerExporterOptions
            {
                ClusterUri = ClusterUri,
                ManagedIdentityId = managedIdentityId,
            },
            new Uri(KustoTokenProvider.DefaultLoginEndpoint));

        Assert.IsType<ManagedIdentityCredential>(credential);
        Assert.EndsWith("|isManagedIdentity:{true}", KustoRestIngestor.GetApplicationName(isManagedIdentity: true), StringComparison.Ordinal);
    }

    [Fact]
    public void Credential_TokenCredentialTakesPrecedence()
    {
        var tokenCredential = new DefaultAzureCredential();
        var credential = KustoRestIngestor.CreateCredential(
            new AzureDataExplorerExporterOptions
            {
                ClusterUri = ClusterUri,
                TokenCredential = tokenCredential,
                UseAzureAuth = true,
                ManagedIdentityId = "system",
            },
            new Uri(KustoTokenProvider.DefaultLoginEndpoint));

        Assert.Same(tokenCredential, credential);
    }

    [Fact]
    public void Credential_UseAzureAuth()
    {
        var credential = KustoRestIngestor.CreateCredential(
            new AzureDataExplorerExporterOptions
            {
                ClusterUri = ClusterUri,
                UseAzureAuth = true,
                ManagedIdentityId = "system",
            },
            new Uri(KustoTokenProvider.DefaultLoginEndpoint));

        Assert.IsType<DefaultAzureCredential>(credential);
    }

    [Theory]
    [InlineData("https://mycluster.westus.kusto.windows.net", "https://mycluster.westus.kusto.windows.net/", "https://ingest-mycluster.westus.kusto.windows.net/")]
    [InlineData("https://ingest-mycluster.westus.kusto.windows.net/", "https://mycluster.westus.kusto.windows.net/", "https://ingest-mycluster.westus.kusto.windows.net/")]
    [InlineData("https://mycluster.kusto.windows.net/some/path?x=1", "https://mycluster.kusto.windows.net/", "https://ingest-mycluster.kusto.windows.net/")]
    [InlineData("http://127.0.0.1:8080", "http://127.0.0.1:8080/", "http://127.0.0.1:8080/")]
    [InlineData("http://localhost:8080", "http://localhost:8080/", "http://localhost:8080/")]
    public void ClusterUri_DerivesEngineAndIngestEndpoints(string clusterUri, string expectedEngine, string expectedIngest)
    {
        Assert.Equal(new Uri(expectedEngine), KustoRestIngestor.GetEngineUri(new Uri(clusterUri)));
        Assert.Equal(new Uri(expectedIngest), KustoRestIngestor.GetIngestUri(new Uri(clusterUri)));
    }
}
