// <copyright file="EntityFrameworkDiagnosticListenerTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Moq;
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

        this.connection = RelationalOptionsExtension.Extract(this.contextOptions).Connection;

        this.Seed();
    }

    [Fact]
    public void EntityFrameworkContextEventsInstrumentedTest()
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor.Object)
            .AddEntityFrameworkCoreInstrumentation().Build();

        using (var context = new ItemsContext(this.contextOptions))
        {
            var items = context.Set<Item>().OrderBy(e => e.Name).ToList();

            Assert.Equal(3, items.Count);
            Assert.Equal("ItemOne", items[0].Name);
            Assert.Equal("ItemThree", items[1].Name);
            Assert.Equal("ItemTwo", items[2].Name);
        }

        Assert.Equal(3, activityProcessor.Invocations.Count);

        var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

        VerifyActivityData(activity);
    }

    [Fact]
    public void EntityFrameworkEnrichDisplayNameWithEnrichWithIDbCommand()
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        var expectedDisplayName = "Text main";
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor.Object)
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

        Assert.Equal(3, activityProcessor.Invocations.Count);

        var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

        VerifyActivityData(activity, altDisplayName: $"{expectedDisplayName}");
    }

    [Fact]
    public void EntityFrameworkContextExceptionEventsInstrumentedTest()
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor.Object)
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
            }
        }

        Assert.Equal(3, activityProcessor.Invocations.Count);

        var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

        VerifyActivityData(activity, isError: true);
    }

    [Fact]
    public void ShouldNotCollectTelemetryWhenFilterEvaluatesToFalseByDbCommand()
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor.Object)
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.Filter = (providerName, command) =>
                {
                    return !command.CommandText.Contains("Item", StringComparison.OrdinalIgnoreCase);
                };
            }).Build();

        using (var context = new ItemsContext(this.contextOptions))
        {
            _ = context.Set<Item>().OrderBy(e => e.Name).ToList();
        }

        Assert.Equal(2, activityProcessor.Invocations.Count);

        var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

        Assert.False(activity.IsAllDataRequested);
        Assert.True(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.None));
    }

    [Fact]
    public void ShouldCollectTelemetryWhenFilterEvaluatesToTrueByDbCommand()
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor.Object)
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.Filter = (providerName, command) =>
                {
                    return command.CommandText.Contains("Item", StringComparison.OrdinalIgnoreCase);
                };
            }).Build();

        using (var context = new ItemsContext(this.contextOptions))
        {
            _ = context.Set<Item>().OrderBy(e => e.Name).ToList();
        }

        Assert.Equal(3, activityProcessor.Invocations.Count);

        var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

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
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor.Object)
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.Filter = (providerName, command) =>
                {
                    return providerName.Equals(provider, StringComparison.OrdinalIgnoreCase);
                };
            }).Build();

        using (var context = new ItemsContext(this.contextOptions))
        {
            _ = context.Set<Item>().OrderBy(e => e.Name).ToList();
        }

        Assert.Equal(2, activityProcessor.Invocations.Count);

        var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

        Assert.False(activity.IsAllDataRequested);
        Assert.True(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.None));
    }

    [Fact]
    public void ShouldCollectTelemetryWhenFilterEvaluatesToTrueByProviderName()
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor.Object)
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.Filter = (providerName, command) =>
                {
                    return providerName.Equals("Microsoft.EntityFrameworkCore.Sqlite", StringComparison.OrdinalIgnoreCase);
                };
            }).Build();

        using (var context = new ItemsContext(this.contextOptions))
        {
            _ = context.Set<Item>().OrderBy(e => e.Name).ToList();
        }

        Assert.Equal(3, activityProcessor.Invocations.Count);

        var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];
        Assert.True(activity.IsAllDataRequested);
        Assert.True(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
    }

    public void Dispose() => this.connection.Dispose();

    private static DbConnection CreateInMemoryDatabase()
    {
        var connection = new SqliteConnection("Filename=:memory:");

        connection.Open();

        return connection;
    }

    private static void VerifyActivityData(Activity activity, bool isError = false, string altDisplayName = null)
    {
        Assert.Equal(altDisplayName ?? "main", activity.DisplayName);

        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal("sqlite", activity.Tags.FirstOrDefault(t => t.Key == EntityFrameworkDiagnosticListener.AttributeDbSystem).Value);

        // TBD: SqlLite not setting the DataSource so it doesn't get set.
        Assert.DoesNotContain(activity.Tags, t => t.Key == EntityFrameworkDiagnosticListener.AttributePeerService);

        Assert.Equal(altDisplayName ?? "main", activity.Tags.FirstOrDefault(t => t.Key == EntityFrameworkDiagnosticListener.AttributeDbName).Value);
        Assert.Equal(CommandType.Text.ToString(), activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.DatabaseStatementTypeKey).Value);

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

        var one = new Item { Name = "ItemOne" };

        var two = new Item { Name = "ItemTwo" };

        var three = new Item { Name = "ItemThree" };

        context.AddRange(one, two, three);

        context.SaveChanges();
    }

    private class Item
    {
        public int Id { get; set; }

        public string Name { get; set; }
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
                    b.Property("Id");
                    b.HasKey("Id");
                    b.Property(e => e.Name);
                });
        }
    }
}
