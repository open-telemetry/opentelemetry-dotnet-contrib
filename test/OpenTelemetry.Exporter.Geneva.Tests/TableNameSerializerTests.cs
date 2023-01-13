// <copyright file="TableNameSerializerTests.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class TableNameSerializerTests
{
    [Theory]
    [InlineData("Unknown", "DefaultLogs")]
    [InlineData("Unknown", "LogTableName", "LogTableName")]
    public void DefaultResolutionTests(string categoryName, string tableName, string defaultTableName = null)
    {
        var mappings = new Dictionary<string, string>();

        if (defaultTableName != null)
        {
            mappings["*"] = defaultTableName;
        }

        var options = new GenevaExporterOptions
        {
            TableNameMappings = mappings,
        };

        RunTableNameSerializerTest(categoryName, tableName, options);
    }

    [Theory]
    [InlineData("Unknown", "Unknown")]
    [InlineData("unknown.table", "Unknowntable")]
    public void PassthroughResolutionTests(string categoryName, string tableName)
    {
        var options = new GenevaExporterOptions
        {
            TableNameMappings = new Dictionary<string, string>()
            {
                ["*"] = "*",
            },
        };

        RunTableNameSerializerTest(categoryName, tableName, options);
    }

    [Theory]
    [InlineData("Unknown", "DefaultLogs")]
    [InlineData("Prefix.Nonmatch", "PrefixNonmatch")]
    [InlineData("Prefix.Sub.Nonmatch", "SubTableName")]
    [InlineData("Prefix.Sub.Final", "FinalTableName")]
    public void PrefixResolutionTests(string categoryName, string tableName)
    {
        var options = new GenevaExporterOptions
        {
            TableNameMappings = new Dictionary<string, string>()
            {
                ["Prefix"] = "*",
                ["Prefix.Sub"] = "SubTableName",
                ["Prefix.Sub.Final"] = "FinalTableName",
            },
        };

        RunTableNameSerializerTest(categoryName, tableName, options);
    }

    [Fact]
    public void ResolvedTableNameCacheTest()
    {
        var options = new GenevaExporterOptions();

        var buffer = new byte[1024];

        var tableNameSerializer = new TableNameSerializer(options, "DefaultLogs");

        Assert.Empty(tableNameSerializer.TableNameCache);

        tableNameSerializer.ResolveAndSerializeTableNameForCategoryName(buffer, 0, "MyCategory", out _);

        Assert.Single(tableNameSerializer.TableNameCache);

        tableNameSerializer.ResolveAndSerializeTableNameForCategoryName(buffer, 0, "MyCategory", out _);

        Assert.Single(tableNameSerializer.TableNameCache);

        tableNameSerializer.ResolveAndSerializeTableNameForCategoryName(buffer, 0, "MyCategory2", out _);

        Assert.Equal(2, tableNameSerializer.TableNameCache.Count);

        tableNameSerializer.ResolveAndSerializeTableNameForCategoryName(buffer, 0, "MyCategory2", out _);

        Assert.Equal(2, tableNameSerializer.TableNameCache.Count);
    }

    private static void RunTableNameSerializerTest(string categoryName, string tableName, GenevaExporterOptions options)
    {
        var buffer = new byte[1024];

        var tableNameSerializer = new TableNameSerializer(options, "DefaultLogs");

        var bytesWritten = tableNameSerializer.ResolveAndSerializeTableNameForCategoryName(buffer, 0, categoryName, out var resolvedTableName);

        Assert.Equal(tableName.Length + 2, bytesWritten);
        Assert.Equal(tableName, resolvedTableName.Item1);
        Assert.Equal(Encoding.ASCII.GetBytes(tableName), resolvedTableName.Item2.AsSpan().Slice(2).ToArray());
    }
}
