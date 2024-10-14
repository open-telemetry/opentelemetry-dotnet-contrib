// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OpenTelemetry.Instrumentation.EntityFrameworkCore.Implementation;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Tests;

public class EntityFrameworkDiagnosticListenerTests : IDisposable
{
    private readonly DbContextOptions<ItemsContext> contextOptions;
    private readonly DbConnection connection;

    public EntityFrameworkDiagnosticListenerTests()
    {
        this.contextOptions = new DbContextOptionsBuilder<ItemsContext>()
            .UseSqlite(CreateInMemoryDatabase())
            .Options;

        this.connection = RelationalOptionsExtension.Extract(this.contextOptions).Connection!;

        this.Seed();
    }

    [Fact]
    public void EntityFrameworkContextEventsInstrumentedTest()
    {
        var exportedItems = new List<Activity>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddEntityFrameworkCoreInstrumentation().Build();

        using (var context = new ItemsContext(this.contextOptions))
        {
            var items = context.Set<Item>().OrderBy(e => e.Name).ToList();

            Assert.Equal(3, items.Count);
            Assert.Equal("ItemOne", items[0].Name);
            Assert.Equal("ItemThree", items[1].Name);
            Assert.Equal("ItemTwo", items[2].Name);
        }

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        VerifyActivityData(activity);
    }

    [Fact]
    public void EntityFrameworkEnrichDisplayNameWithEnrichWithIDbCommand()
    {
        var exportedItems = new List<Activity>();
        var expectedDisplayName = "Text main";
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.EnrichWithIDbCommand = (activity1, command) =>
                {
                    var stateDisplayName = $"{command.CommandType} main";
                    activity1.DisplayName = stateDisplayName;
                    activity1.SetTag("db.name", stateDisplayName);
                };
            }).Build();

        using (var context = new ItemsContext(this.contextOptions))
        {
            var items = context.Set<Item>().OrderBy(e => e.Name).ToList();

            Assert.Equal(3, items.Count);
            Assert.Equal("ItemOne", items[0].Name);
            Assert.Equal("ItemThree", items[1].Name);
            Assert.Equal("ItemTwo", items[2].Name);
        }

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        VerifyActivityData(activity, altDisplayName: $"{expectedDisplayName}");
    }

    [Fact]
    public void EntityFrameworkEnrichDisplayNameWithEnrichWithIDbCommand_New()
    {
        var exportedItems = new List<Activity>();
        var expectedDisplayName = "Text main";
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.EnrichWithIDbCommand = (activity1, command) =>
                {
                    var stateDisplayName = $"{command.CommandType} main";
                    activity1.DisplayName = stateDisplayName;
                    activity1.SetTag("db.namespace", stateDisplayName);
                };
                options.EmitNewAttributes = true;
            }).Build();

        using (var context = new ItemsContext(this.contextOptions))
        {
            var items = context.Set<Item>().OrderBy(e => e.Name).ToList();

            Assert.Equal(3, items.Count);
            Assert.Equal("ItemOne", items[0].Name);
            Assert.Equal("ItemThree", items[1].Name);
            Assert.Equal("ItemTwo", items[2].Name);
        }

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        VerifyActivityData(activity, altDisplayName: $"{expectedDisplayName}", emitNewAttributes: true);
    }

    [Fact]
    public void EntityFrameworkContextExceptionEventsInstrumentedTest()
    {
        var exportedItems = new List<Activity>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddEntityFrameworkCoreInstrumentation()
            .Build();

        using (var context = new ItemsContext(this.contextOptions))
        {
            try
            {
                context.Database.ExecuteSqlRaw("select * from no_table");
            }
            catch
            {
                // intentional empty catch
            }
        }

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        VerifyActivityData(activity, isError: true);
    }

    [Fact]
    public void ShouldNotCollectTelemetryWhenFilterEvaluatesToFalseByDbCommand()
    {
        var exportedItems = new List<Activity>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.Filter = (_, command) => !command.CommandText.Contains("Item", StringComparison.OrdinalIgnoreCase);
            }).Build();

        using (var context = new ItemsContext(this.contextOptions))
        {
            _ = context.Set<Item>().OrderBy(e => e.Name).ToList();
        }

        Assert.Empty(exportedItems);
    }

    [Fact]
    public void ShouldCollectTelemetryWhenFilterEvaluatesToTrueByDbCommand()
    {
        var exportedItems = new List<Activity>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.Filter = (_, command) => command.CommandText.Contains("Item", StringComparison.OrdinalIgnoreCase);
            }).Build();

        using (var context = new ItemsContext(this.contextOptions))
        {
            _ = context.Set<Item>().OrderBy(e => e.Name).ToList();
        }

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        Assert.True(activity.IsAllDataRequested);
        Assert.True(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Microsoft.EntityFrameworkCore.Cosmos")]
    [InlineData("Devart.Data.SQLite.EFCore")]
    [InlineData("MySql.Data.EntityFrameworkCore")]
    [InlineData("Pomelo.EntityFrameworkCore.MySql")]
    [InlineData("Devart.Data.MySql.EFCore")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData("Devart.Data.PostgreSql.EFCore")]
    [InlineData("Oracle.EntityFrameworkCore")]
    [InlineData("Devart.Data.Oracle.EFCore")]
    [InlineData("Microsoft.EntityFrameworkCore.InMemory")]
    [InlineData("FirebirdSql.EntityFrameworkCore.Firebird")]
    [InlineData("FileContextCore")]
    [InlineData("EntityFrameworkCore.SqlServerCompact35")]
    [InlineData("EntityFrameworkCore.SqlServerCompact40")]
    [InlineData("EntityFrameworkCore.OpenEdge")]
    [InlineData("EntityFrameworkCore.Jet")]
    [InlineData("Google.Cloud.EntityFrameworkCore.Spanner")]
    [InlineData("Teradata.EntityFrameworkCore")]
    public void ShouldNotCollectTelemetryWhenFilterEvaluatesToFalseByProviderName(string provider)
    {
        var exportedItems = new List<Activity>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.Filter = (providerName, _) => providerName != null && providerName.Equals(provider, StringComparison.OrdinalIgnoreCase);
            }).Build();

        using (var context = new ItemsContext(this.contextOptions))
        {
            _ = context.Set<Item>().OrderBy(e => e.Name).ToList();
        }

        Assert.Empty(exportedItems);
    }

    [Fact]
    public void ShouldCollectTelemetryWhenFilterEvaluatesToTrueByProviderName()
    {
        var exportedItems = new List<Activity>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.Filter = (providerName, _) => providerName != null && providerName.Equals("Microsoft.EntityFrameworkCore.Sqlite", StringComparison.OrdinalIgnoreCase);
            }).Build();

        using (var context = new ItemsContext(this.contextOptions))
        {
            _ = context.Set<Item>().OrderBy(e => e.Name).ToList();
        }

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        Assert.True(activity.IsAllDataRequested);
        Assert.True(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
    }

    public void Dispose() => this.connection.Dispose();

    private static SqliteConnection CreateInMemoryDatabase()
    {
        var connection = new SqliteConnection("Filename=:memory:");

        connection.Open();

        return connection;
    }

    private static void VerifyActivityData(Activity activity, bool isError = false, string? altDisplayName = null, bool emitNewAttributes = false)
    {
        Assert.Equal(altDisplayName ?? "main", activity.DisplayName);

        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal("sqlite", activity.Tags.FirstOrDefault(t => t.Key == EntityFrameworkDiagnosticListener.AttributeDbSystem).Value);

        // TBD: SqlLite not setting the DataSource so it doesn't get set.
        Assert.DoesNotContain(activity.Tags, t => t.Key == EntityFrameworkDiagnosticListener.AttributePeerService);

        if (!emitNewAttributes)
        {
            Assert.Equal(altDisplayName ?? "main", activity.Tags.FirstOrDefault(t => t.Key == EntityFrameworkDiagnosticListener.AttributeDbName).Value);
        }

        if (emitNewAttributes)
        {
            Assert.Equal(altDisplayName ?? "main", activity.Tags.FirstOrDefault(t => t.Key == EntityFrameworkDiagnosticListener.AttributeDbNamespace).Value);
        }

        if (!isError)
        {
            Assert.Equal(Status.Unset, activity.GetStatus());
        }
        else
        {
            Status status = activity.GetStatus();
            Assert.Equal(StatusCode.Error, status.StatusCode);
            Assert.Equal("SQLite Error 1: 'no such table: no_table'.", status.Description);
            Assert.Contains(activity.Tags, t => t.Key == SpanAttributeConstants.StatusDescriptionKey);
        }
    }

    private void Seed()
    {
        using var context = new ItemsContext(this.contextOptions);

        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        var one = new Item("ItemOne");

        var two = new Item("ItemTwo");

        var three = new Item("ItemThree");

        context.AddRange(one, two, three);

        context.SaveChanges();
    }

    private class Item
    {
        public Item(string name)
        {
            this.Name = name;
        }

        public string Name { get; }
    }

    private class ItemsContext : DbContext
    {
        public ItemsContext(DbContextOptions options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Item>(
                b =>
                {
                    b.Property(e => e.Name);
                    b.HasKey("Name");
                });
        }
    }
}
