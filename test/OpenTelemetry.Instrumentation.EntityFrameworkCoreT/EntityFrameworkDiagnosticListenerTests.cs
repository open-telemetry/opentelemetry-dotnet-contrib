﻿// <copyright file="EntityFrameworkDiagnosticListenerTests.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Tests
{
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
            var activityProcessor = new Mock<ActivityProcessor>();
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

            Assert.Equal(2, activityProcessor.Invocations.Count);

            var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

            VerifyActivityData(activity);
        }

        [Fact]
        public void EntityFrameworkContextExceptionEventsInstrumentedTest()
        {
            var activityProcessor = new Mock<ActivityProcessor>();
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

            Assert.Equal(2, activityProcessor.Invocations.Count);

            var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

            VerifyActivityData(activity, isError: true);
        }

        public void Dispose() => this.connection.Dispose();

        private static DbConnection CreateInMemoryDatabase()
        {
            var connection = new SqliteConnection("Filename=:memory:");

            connection.Open();

            return connection;
        }

        private static void VerifyActivityData(Activity activity, bool isError = false)
        {
            Assert.Equal("main", activity.DisplayName);
            Assert.Equal(ActivityKind.Client, activity.Kind);
            Assert.Equal("sqlite", activity.Tags.FirstOrDefault(t => t.Key == EntityFrameworkDiagnosticListener.AttributeDbSystem).Value);

            // TBD: SqlLite not setting the DataSource so it doesn't get set.
            Assert.DoesNotContain(activity.Tags, t => t.Key == EntityFrameworkDiagnosticListener.AttributePeerService);

            Assert.Equal("main", activity.Tags.FirstOrDefault(t => t.Key == EntityFrameworkDiagnosticListener.AttributeDbName).Value);
            Assert.Equal(CommandType.Text.ToString(), activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.DatabaseStatementTypeKey).Value);

            if (!isError)
            {
                Assert.Equal("Ok", activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.StatusCodeKey).Value);
            }
            else
            {
                Assert.Equal("Unknown", activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.StatusCodeKey).Value);
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
}
