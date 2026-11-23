// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Kusto.Data;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.AzureDataExplorer.Tests;

/// <summary>
/// Opt-in tests against a real Azure Data Explorer cluster. They are skipped unless
/// the <c>OTEL_ADX_TEST_CLUSTER_URI</c> environment variable is set. See the test
/// project README for the required setup.
/// </summary>
[Collection(IntegrationTestsCollectionDefinition.Name)]
[Trait("CategoryName", "AzureDataExplorerClusterTests")]
public sealed class AzureDataExplorerClusterTests
{
    private const string ClusterUriVariable = "OTEL_ADX_TEST_CLUSTER_URI";
    private const string DatabaseVariable = "OTEL_ADX_TEST_DATABASE";
    private const string ApplicationIdVariable = "OTEL_ADX_TEST_APPLICATION_ID";
    private const string ApplicationKeyVariable = "OTEL_ADX_TEST_APPLICATION_KEY";
    private const string TenantIdVariable = "OTEL_ADX_TEST_TENANT_ID";
    private const string ManagedIdentityIdVariable = "OTEL_ADX_TEST_MANAGED_IDENTITY_ID";
    private const string TimeoutVariable = "OTEL_ADX_TEST_TIMEOUT_MINUTES";

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    [SkipUnlessEnvVarFoundTheory(ClusterUriVariable)]
    [InlineData(AzureDataExplorerIngestionType.Queued)]
    [InlineData(AzureDataExplorerIngestionType.Managed)]
    public async Task AllSignalsAreIngestedIntoCluster(AzureDataExplorerIngestionType ingestionType)
    {
        void Configure(AzureDataExplorerExporterOptions options)
        {
            ConfigureFromEnvironment(options);
            options.IngestionType = ingestionType;
        }

        var queryOptions = new AzureDataExplorerExporterOptions();
        ConfigureFromEnvironment(queryOptions);
        queryOptions.ClusterUri = GetEngineUri(queryOptions.ClusterUri!);

        var credential = KustoRestIngestor.CreateCredential(queryOptions, new Uri(KustoTokenProvider.DefaultLoginEndpoint));
        var kcsb = new KustoConnectionStringBuilder(queryOptions.ClusterUri.OriginalString.TrimEnd('/'))
            .WithAadAzureTokenCredentialsAuthentication(credential);
        using var client = new KustoTestClient(kcsb, queryOptions.DatabaseName);

        var timeoutMinutes = GetVariable(TimeoutVariable);
        var timeout = TimeSpan.FromMinutes(timeoutMinutes != null ? double.Parse(timeoutMinutes, CultureInfo.InvariantCulture) : 10);

        // Queued ingestion is batched by the service, so wait for all signals concurrently.
        await Task.WhenAll(
            IntegrationScenarios.ExportAndVerifyTracesAsync(client, Configure, timeout, PollInterval),
            IntegrationScenarios.ExportAndVerifyMetricsAsync(client, Configure, timeout, PollInterval),
            IntegrationScenarios.ExportAndVerifyLogsAsync(client, Configure, timeout, PollInterval));
    }

    private static void ConfigureFromEnvironment(AzureDataExplorerExporterOptions options)
    {
        options.ClusterUri = new Uri(GetVariable(ClusterUriVariable)!);
        options.DatabaseName = GetVariable(DatabaseVariable) ?? AzureDataExplorerExporterOptions.DefaultDatabaseName;

        var applicationId = GetVariable(ApplicationIdVariable);
        var managedIdentityId = GetVariable(ManagedIdentityIdVariable);
        if (applicationId != null)
        {
            options.ApplicationId = applicationId;
            options.ApplicationKey = GetVariable(ApplicationKeyVariable);
            options.TenantId = GetVariable(TenantIdVariable);
        }
        else if (managedIdentityId != null)
        {
            options.ManagedIdentityId = managedIdentityId;
        }
        else
        {
            // DefaultAzureCredential, e.g. the identity signed in with `az login`.
            options.UseAzureAuth = true;
        }
    }

    private static Uri GetEngineUri(Uri clusterUri)
    {
        var builder = new UriBuilder(clusterUri);
        if (builder.Host.StartsWith("ingest-", StringComparison.OrdinalIgnoreCase))
        {
            builder.Host = builder.Host.Substring("ingest-".Length);
        }

        return builder.Uri;
    }

    private static string? GetVariable(string name)
    {
        var value = SkipUnlessEnvVarFoundTheoryAttribute.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
