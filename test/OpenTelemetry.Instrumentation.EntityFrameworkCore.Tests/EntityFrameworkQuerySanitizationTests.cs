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

        // The query executed against SQLite contains the column "Nom de l'enterprise"...
        Assert.Contains("\"a\".\"Nom de l'enterprise\"", queryText, StringComparison.Ordinal);

        // ...and the API key literal is replaced by the placeholder.
        Assert.DoesNotContain(ApiKey, queryText, StringComparison.Ordinal);
        Assert.Contains("\"a\".\"ApiKey\" = ?", queryText, StringComparison.Ordinal);

        // Control: the same query shape without the apostrophe.
        Assert.DoesNotContain(ApiKey, control, StringComparison.Ordinal);
        Assert.Contains("\"p\".\"ApiKey\" = ?", control, StringComparison.Ordinal);
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

    [Theory]
    [InlineData("ALTER USER app_reader IDENTIFIED BY Tr0ub4dor3xQ9", "ALTER USER ? IDENTIFIED BY ?")]
    [InlineData("CREATE USER app_writer IDENTIFIED BY \"P@ss#w0rd!Zk7\" DEFAULT TABLESPACE users", "CREATE USER ? IDENTIFIED BY \"?\" DEFAULT TABLESPACE users")]
    [InlineData("ALTER USER app_reader IDENTIFIED BY NewPw_2026 REPLACE OldPw_2025", "ALTER USER ? IDENTIFIED BY ? REPLACE ?")]
    public void OracleProvider_PasswordWhichIsNotALiteral_IsRedactedFromQueryText(string commandText, string expected)
    {
        var exported = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(exported)
                   .AddEntityFrameworkCoreInstrumentation()
                   .Build())
        {
            // The Oracle provider is not referenced, so the SQLite provider reports its name instead.
            RenamedSqliteDatabaseProvider.NameOverride = "Oracle.EntityFrameworkCore";

            using var context = new AccountsContext(OfflineOptions(b =>
            {
                b.UseSqlite("Data Source=:memory:");
#pragma warning disable EF1001 // Internal EF Core API usage.
                b.ReplaceService<IDatabaseProvider, DatabaseProvider<SqliteOptionsExtension>, RenamedSqliteDatabaseProvider>();
#pragma warning restore EF1001 // Internal EF Core API usage.
            }));

            context.Database.ExecuteSqlRaw(commandText);
        }

        var activity = Assert.Single(exported);
        var queryText = this.GetQueryText(activity);

        Assert.Equal("oracle", activity.GetTagValue(SemanticConventions.AttributeDbSystem));
        Assert.Equal(expected, queryText);
    }

    [Theory]
    [InlineData("SELECT Id, Name AS ['Tis the season] FROM Accounts WHERE ApiKey = 'sk_test_hunter-two'", "SELECT Id, Name AS ['Tis the season] FROM Accounts WHERE ApiKey = ?")]
    [InlineData("UPDATE Accounts SET Verified = 1 WHERE[Mother's Maiden Name] = 'hunter-two'", "UPDATE Accounts SET Verified = ? WHERE[Mother's Maiden Name] = ?")]
    public void SqliteProvider_BracketedIdentifierWithApostrophe_IsNotParsedAsSql(string commandText, string expected)
    {
        var exported = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(exported)
                   .AddEntityFrameworkCoreInstrumentation()
                   .Build())
        {
            using var context = new AccountsContext(OfflineOptions(b => b.UseSqlite("Data Source=:memory:")));

            context.Database.ExecuteSqlRaw(commandText);
        }

        var activity = Assert.Single(exported);
        var queryText = this.GetQueryText(activity);

        Assert.DoesNotContain("hunter-two", queryText, StringComparison.Ordinal);
        Assert.Equal(expected, queryText);
    }

    [Theory]
    [InlineData("BEGIN $IF $$DEBUG $THEN dbms_output.put_line('hunter-two'); $END END;", "BEGIN $IF $$DEBUG $THEN dbms_output.put_line(?); $END END;")]
    public void OracleProvider_ConditionalCompilationDirective_IsNotAStringInDollarQuotes(string commandText, string expected)
    {
        var exported = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(exported)
                   .AddEntityFrameworkCoreInstrumentation()
                   .Build())
        {
            // The Oracle provider is not referenced, so the SQLite provider reports its name instead.
            RenamedSqliteDatabaseProvider.NameOverride = "Oracle.EntityFrameworkCore";

            using var context = new AccountsContext(OfflineOptions(b =>
            {
                b.UseSqlite("Data Source=:memory:");
#pragma warning disable EF1001 // Internal EF Core API usage.
                b.ReplaceService<IDatabaseProvider, DatabaseProvider<SqliteOptionsExtension>, RenamedSqliteDatabaseProvider>();
#pragma warning restore EF1001 // Internal EF Core API usage.
            }));

            context.Database.ExecuteSqlRaw(commandText);
        }

        var activity = Assert.Single(exported);
        var queryText = this.GetQueryText(activity);

        Assert.Equal("oracle", activity.GetTagValue(SemanticConventions.AttributeDbSystem));
        Assert.DoesNotContain("hunter-two", queryText, StringComparison.Ordinal);
        Assert.Equal(expected, queryText);
    }

    [Fact]
    public void SqliteProvider_SlashAsteriskInBlockComment_DoesNotAffectLaterLiterals()
    {
        var exported = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(exported)
                   .AddEntityFrameworkCoreInstrumentation()
                   .Build())
        {
            using var context = new AccountsContext(OfflineOptions(b => b.UseSqlite("Data Source=:memory:")));

            // SQLite ends a block comment at the first */, so the comment ends before SELECT.
            context.Database.ExecuteSqlRaw(
                "/* served on GET /notes/* */ UPDATE Accounts SET Note = '*/ ends nothing' WHERE ApiKey = 'sk_test_hunter-two'");
        }

        var activity = Assert.Single(exported);
        var queryText = this.GetQueryText(activity);

        Assert.DoesNotContain("hunter-two", queryText, StringComparison.Ordinal);
        Assert.Equal("UPDATE Accounts SET Note = ? WHERE ApiKey = ?", queryText.Trim());
    }

    [Fact]
    public void PostgresProvider_NestedBlockComment_IsSkippedAsAWhole()
    {
        var exported = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(exported)
                   .AddEntityFrameworkCoreInstrumentation()
                   .Build())
        {
            using var context = new AccountsContext(OfflineOptions(b => b.UseNpgsql("Host=pg.internal;Database=crm")));

            // PostgreSQL nests block comments, so the apostrophe is part of the comment.
            context.Database.ExecuteSqlRaw(
                "/* a /* b */ don't */ UPDATE Accounts SET Note = 'x' WHERE ApiKey = 'sk_test_hunter-two'");
        }

        var activity = Assert.Single(exported);
        var queryText = this.GetQueryText(activity);

        Assert.DoesNotContain("hunter-two", queryText, StringComparison.Ordinal);
        Assert.Equal("UPDATE Accounts SET Note = ? WHERE ApiKey = ?", queryText.Trim());
    }

    [Theory]
    [InlineData("UPDATE Cases SET Note = '''The customer's recovery code is hunter-two''' WHERE CaseId = 4711", "UPDATE Cases SET Note = ? WHERE CaseId = ?")]
    [InlineData("UPDATE Customers SET LastName = 'O\\'Brien', AccessCode = 'hunter-two' WHERE CustomerId = 7", "UPDATE Customers SET LastName = ?, AccessCode = ? WHERE CustomerId = ?")]
    [InlineData("SELECT * FROM Customers WHERE Email = \"hunter-two@example.com\"", "SELECT * FROM Customers WHERE Email = ?")]
    public void SpannerProvider_GoogleSqlStringLiterals_AreRedactedFromQueryText(string commandText, string expected)
    {
        var exported = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(exported)
                   .AddEntityFrameworkCoreInstrumentation()
                   .Build())
        {
            // The Spanner provider is not referenced, so the SQLite provider reports its name instead.
            RenamedSqliteDatabaseProvider.NameOverride = "Google.Cloud.EntityFrameworkCore.Spanner";

            using var context = new AccountsContext(OfflineOptions(b =>
            {
                b.UseSqlite("Data Source=:memory:");
#pragma warning disable EF1001 // Internal EF Core API usage.
                b.ReplaceService<IDatabaseProvider, DatabaseProvider<SqliteOptionsExtension>, RenamedSqliteDatabaseProvider>();
#pragma warning restore EF1001 // Internal EF Core API usage.
            }));

            context.Database.ExecuteSqlRaw(commandText);
        }

        var activity = Assert.Single(exported);
        var queryText = this.GetQueryText(activity);

        Assert.Equal("spanner", activity.GetTagValue(SemanticConventions.AttributeDbSystem));
        Assert.DoesNotContain("hunter-two", queryText, StringComparison.Ordinal);
        Assert.Equal(expected, queryText);
    }

    [Theory]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL", "SELECT $t\u00e4g$secret-name$t\u00e4g$", "SELECT ?")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL", "CREATE USER a\u0301lice WITH PASSWORD 'secret-name'", "CREATE USER ? WITH PASSWORD ?")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL", "SELECT 0b101010, 0o173, 1_000_000", "SELECT ?, ?, ?")]
    [InlineData("Pomelo.EntityFrameworkCore.MySql", "CREATE USER alice IDENTIFIED BY 'secret-name', bob IDENTIFIED BY 'other'", "CREATE USER ? IDENTIFIED BY ?, ? IDENTIFIED BY ?")]
    [InlineData("Pomelo.EntityFrameworkCore.MySql", "SELECT $$col FROM t WHERE a = '$$' AND b = 'secret-name'", "SELECT $$col FROM t WHERE a = ? AND b = ?")]
    [InlineData("Oracle.EntityFrameworkCore", "ALTER USER alice IDENTIFIED BY p\u0301assword", "ALTER USER ? IDENTIFIED BY ?")]
    [InlineData("Google.Cloud.EntityFrameworkCore.Spanner", "SELECT `a\\`b` FROM t WHERE a = '`' AND b = 'secret-name'", "SELECT `a\\`b` FROM t WHERE a = ? AND b = ?")]
    [InlineData("Microsoft.EntityFrameworkCore.Sqlite", "SELECT $tag$col FROM t WHERE a = '$tag$' AND b = 'secret-name'", "SELECT $tag$col FROM t WHERE a = ? AND b = ?")]
    public void ProviderDialect_RedactsExtendedLiteralAndAccountForms(string providerName, string sql, string expected)
    {
        var exported = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(exported)
                   .AddEntityFrameworkCoreInstrumentation()
                   .Build())
        {
            RenamedSqliteDatabaseProvider.NameOverride = providerName;

            using var context = new AccountsContext(OfflineOptions(b =>
            {
                b.UseSqlite("Data Source=:memory:");
#pragma warning disable EF1001 // Internal EF Core API usage.
                b.ReplaceService<IDatabaseProvider, DatabaseProvider<SqliteOptionsExtension>, RenamedSqliteDatabaseProvider>();
#pragma warning restore EF1001 // Internal EF Core API usage.
            }));

            context.Database.ExecuteSqlRaw(sql);
        }

        var activity = Assert.Single(exported);
        Assert.Equal(expected, this.GetQueryText(activity));
        Assert.DoesNotContain("secret-name", activity.DisplayName, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-name", activity.GetTagValue(SemanticConventions.AttributeDbQuerySummary)?.ToString() ?? string.Empty, StringComparison.Ordinal);
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

        [Column("Nom de l'enterprise")]
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
