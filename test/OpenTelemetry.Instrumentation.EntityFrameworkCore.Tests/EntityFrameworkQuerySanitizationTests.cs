// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Sqlite.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Tests;

public sealed class EntityFrameworkQuerySanitizationTests : IDisposable
{
    private const string ApiKey = "sk_live_51HqLyjWDarjtT1zdp7dc";

    private readonly SqliteConnection connection;
    private readonly ITestOutputHelper output;

    public EntityFrameworkQuerySanitizationTests(ITestOutputHelper output)
    {
        this.output = output;
        this.connection = new SqliteConnection("Filename=:memory:");
        this.connection.Open();

        using var context = CreateSqliteContext(this.connection);
        context.Database.EnsureCreated();
        context.Accounts.Add(new() { Company = "Contoso", ApiKey = ApiKey });
        context.PlainAccounts.Add(new() { Company = "Contoso", ApiKey = ApiKey });
        context.SaveChanges();
    }

    public void Dispose() => this.connection.Dispose();

    [Fact]
    public void ApostropheInMappedColumnName_InlinedConstantIsRedactedFromExportedQueryText()
    {
        var exported = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(exported)
                   .AddEntityFrameworkCoreInstrumentation()
                   .Build())
        {
            using var context = CreateSqliteContext(this.connection);

            var apiKey = ApiKey;
            Assert.Single(context.Accounts.Where(a => a.ApiKey == EF.Constant(apiKey)).ToList());
            Assert.Single(context.PlainAccounts.Where(a => a.ApiKey == EF.Constant(apiKey)).ToList());
        }

        Assert.Equal(2, exported.Count);

        var queryText = this.GetQueryText(exported[0]);
        var control = this.GetQueryText(exported[1]);

        // The query executed against SQLite contains the column "Nom de l'entreprise"...
        Assert.Contains("\"a\".\"Nom de l'entreprise\"", queryText, StringComparison.Ordinal);

        // ...and the API key literal is replaced by the placeholder.
        Assert.DoesNotContain(ApiKey, queryText, StringComparison.Ordinal);
        Assert.Contains("\"a\".\"ApiKey\" = ?", queryText, StringComparison.Ordinal);

        // Control: the same query shape without the apostrophe.
        Assert.DoesNotContain(ApiKey, control, StringComparison.Ordinal);
        Assert.Contains("\"p\".\"ApiKey\" = ?", control, StringComparison.Ordinal);
    }

    [Fact]
    public void MySqlProvider_ContainsOverCapturedList_WithApostropheColumn_RedactsEveryListValue()
    {
        var exported = new List<Activity>();
        string[] emails = ["alice@example.com", "bob@example.com"];

        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(exported)
                   .AddEntityFrameworkCoreInstrumentation()
                   .Build())
        {
            using var context = new AccountsContext(OfflineOptions(b => b.UseMySql("Server=mysql.internal;Database=crm", new MySqlServerVersion(new Version(8, 0, 36)))));

            _ = context.Accounts.Where(a => emails.Contains(a.OwnerEmail)).ToList();
            _ = context.PlainAccounts.Where(a => emails.Contains(a.OwnerEmail)).ToList();
        }

        Assert.Equal(2, exported.Count);

        var queryText = this.GetQueryText(exported[0]);
        var control = this.GetQueryText(exported[1]);

        Assert.Equal("mysql", exported[0].GetTagValue(SemanticConventions.AttributeDbSystem));
        Assert.Contains("`a`.`Nom de l'entreprise`", queryText, StringComparison.Ordinal);
        Assert.DoesNotContain("@example.com", queryText, StringComparison.Ordinal);
        Assert.Contains("IN (?)", queryText, StringComparison.Ordinal);

        // Control: the same values are redacted without the apostrophe.
        Assert.DoesNotContain("@example.com", control, StringComparison.Ordinal);
        Assert.Contains("IN (?)", control, StringComparison.Ordinal);
    }

    [Fact]
    public void MySqlProvider_RawSqlInListWithDoubleQuotedValueContainingParen_RedactsValues()
    {
        var exported = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(exported)
                   .AddEntityFrameworkCoreInstrumentation()
                   .Build())
        {
            using var context = new AccountsContext(OfflineOptions(b => b.UseMySql("Server=mysql.internal;Database=crm", new MySqlServerVersion(new Version(8, 0, 36)))));

            context.Database.ExecuteSqlRaw(
                "UPDATE `PlainAccounts` SET `Company` = 'churned' WHERE `OwnerEmail` IN ('carol@example.com', \"Fabrikam (UK)\", \"alice@example.com\", \"x\", \"bob@example.com\")");
            context.Database.ExecuteSqlRaw(
                "UPDATE `PlainAccounts` SET `Company` = 'churned' WHERE `OwnerEmail` IN ('carol@example.com', 'Fabrikam (UK)', 'alice@example.com', 'x', 'bob@example.com')");
        }

        Assert.Equal(2, exported.Count);

        const string Expected = "UPDATE `PlainAccounts` SET `Company` = ? WHERE `OwnerEmail` IN (?)";

        Assert.Equal(Expected, this.GetQueryText(exported[0]));
        Assert.Equal(Expected, this.GetQueryText(exported[1]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PostgresProvider_UnnestArrayLiteralsInFromClause_AreRedactedFromQueryTextAndSpanName(bool emitNewAttributes)
    {
        var exported = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(exported)
                   .AddEntityFrameworkCoreInstrumentation(o =>
                   {
                       o.EmitOldAttributes = !emitNewAttributes;
                       o.EmitNewAttributes = emitNewAttributes;
                   })
                   .Build())
        {
            using var context = new AccountsContext(OfflineOptions(b => b.UseNpgsql("Host=pg.internal;Database=crm")));

            context.Database.ExecuteSqlRaw(
                "INSERT INTO audit_recipients (email) SELECT e FROM unnest(ARRAY['carol@example.com', 'alice@example.com', 'bob@example.com']) AS t(e)");

            // Control: the same array constructor outside of a FROM clause.
            context.Database.ExecuteSqlRaw(
                "INSERT INTO audit_recipients (email) SELECT unnest(ARRAY['carol@example.com', 'alice@example.com', 'bob@example.com'])");
        }

        Assert.Equal(2, exported.Count);

        var activity = exported[0];
        var queryText = this.GetQueryText(activity);

        Assert.Equal("postgresql", activity.GetTagValue(emitNewAttributes ? SemanticConventions.AttributeDbSystemName : SemanticConventions.AttributeDbSystem));
        Assert.Equal("INSERT INTO audit_recipients (email) SELECT e FROM unnest(ARRAY[?, ?, ?]) AS t(e)", queryText);
        Assert.DoesNotContain("@example.com", activity.DisplayName, StringComparison.Ordinal);

        if (emitNewAttributes)
        {
            Assert.Equal("INSERT audit_recipients SELECT unnest", activity.GetTagValue(SemanticConventions.AttributeDbQuerySummary));
            Assert.Equal("INSERT audit_recipients SELECT unnest", activity.DisplayName);
        }

        var control = this.GetQueryText(exported[1]);
        Assert.DoesNotContain("@example.com", control, StringComparison.Ordinal);
        Assert.DoesNotContain("@example.com", exported[1].DisplayName, StringComparison.Ordinal);
    }

    private static AccountsContext CreateSqliteContext(SqliteConnection connection, bool renameProvider = false)
    {
        var builder = new DbContextOptionsBuilder<AccountsContext>().UseSqlite(connection);

        if (renameProvider)
        {
#pragma warning disable EF1001 // Internal EF Core API usage.
            builder.ReplaceService<IDatabaseProvider, DatabaseProvider<SqliteOptionsExtension>, RenamedSqliteDatabaseProvider>();
#pragma warning restore EF1001 // Internal EF Core API usage.
        }

        return new AccountsContext(builder.Options);
    }

    private static DbContextOptions<AccountsContext> OfflineOptions(Action<DbContextOptionsBuilder<AccountsContext>> configure)
    {
        var builder = new DbContextOptionsBuilder<AccountsContext>();
        configure(builder);
        builder.AddInterceptors(new OfflineConnectionInterceptor(), new OfflineCommandInterceptor());
        return builder.Options;
    }

    private string GetQueryText(Activity activity)
    {
        var queryText = (activity.GetTagValue(SemanticConventions.AttributeDbStatement) ?? activity.GetTagValue(SemanticConventions.AttributeDbQueryText)) as string
            ?? throw new InvalidOperationException("No query text was exported.");

        this.output.WriteLine($"span name: {activity.DisplayName}");
        this.output.WriteLine($"exported query text: {queryText}");

        return queryText;
    }

#pragma warning disable EF1001 // Internal EF Core API usage.
    internal sealed class RenamedSqliteDatabaseProvider(DatabaseProviderDependencies dependencies)
        : DatabaseProvider<SqliteOptionsExtension>(dependencies)
#pragma warning restore EF1001 // Internal EF Core API usage.
    {
        public static string NameOverride { get; set; } = "EntityFrameworkCore.Jet";

        public override string Name => NameOverride;
    }

    internal sealed class AccountsContext(DbContextOptions<AccountsContext> options) : DbContext(options)
    {
        public DbSet<ApostropheAccount> Accounts { get; set; } = null!;

        public DbSet<PlainAccount> PlainAccounts { get; set; } = null!;
    }

    internal sealed class ApostropheAccount
    {
        public int Id { get; set; }

        [Column("Nom de l'entreprise")]
        public string Company { get; set; } = default!;

        public string OwnerEmail { get; set; } = "owner@example.com";

        public string ApiKey { get; set; } = default!;
    }

    internal sealed class PlainAccount
    {
        public int Id { get; set; }

        public string Company { get; set; } = default!;

        public string OwnerEmail { get; set; } = "owner@example.com";

        public string ApiKey { get; set; } = default!;
    }

    private sealed class OfflineConnectionInterceptor : DbConnectionInterceptor
    {
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
            => InterceptionResult.Suppress();

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => new(InterceptionResult.Suppress());

        public override InterceptionResult ConnectionClosing(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
            => InterceptionResult.Suppress();

        public override ValueTask<InterceptionResult> ConnectionClosingAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
            => new(InterceptionResult.Suppress());
    }

    private sealed class OfflineCommandInterceptor : DbCommandInterceptor
    {
#pragma warning disable CA2000 // Dispose objects before losing scope: ownership passes to EF Core.
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
            => InterceptionResult<DbDataReader>.SuppressWithResult(new DataTable().CreateDataReader());
#pragma warning restore CA2000 // Dispose objects before losing scope

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
            => InterceptionResult<int>.SuppressWithResult(1);
    }
}
