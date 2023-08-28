// <copyright file="ExtensionFieldInformationManagerTests.cs" company="OpenTelemetry Authors">
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

using Xunit;

namespace OpenTelemetry.Exporter.OneCollector.Tests;

public class ExtensionFieldInformationManagerTests
{
    [Fact]
    public void FieldInformationIsCachedTest()
    {
        var extensionFieldInformationManager = new ExtensionFieldInformationManager();

        var result = extensionFieldInformationManager.TryResolveExtensionFieldInformation("ext.something.fieldName1", out var fieldInformation);

        Assert.True(result);
        Assert.NotNull(fieldInformation);
        Assert.Equal("something", fieldInformation.ExtensionName);
        Assert.Equal("fieldName1", fieldInformation.FieldName);

        Assert.Equal(1, extensionFieldInformationManager.CountOfCachedExtensionFields);

        result = extensionFieldInformationManager.TryResolveExtensionFieldInformation("ext.something.fieldName1", out fieldInformation);

        Assert.Equal(1, extensionFieldInformationManager.CountOfCachedExtensionFields);

        Assert.True(result);
        Assert.NotNull(fieldInformation);
        Assert.Equal("something", fieldInformation.ExtensionName);
        Assert.Equal("fieldName1", fieldInformation.FieldName);

        result = extensionFieldInformationManager.TryResolveExtensionFieldInformation("ext.something.field.Name2", out fieldInformation);

        Assert.Equal(2, extensionFieldInformationManager.CountOfCachedExtensionFields);

        Assert.True(result);
        Assert.NotNull(fieldInformation);
        Assert.Equal("something", fieldInformation.ExtensionName);
        Assert.Equal("field.Name2", fieldInformation.FieldName);

        result = extensionFieldInformationManager.TryResolveExtensionFieldInformation("ext.extra", out fieldInformation);

        Assert.Equal(3, extensionFieldInformationManager.CountOfCachedExtensionFields);

        Assert.True(result);
        Assert.NotNull(fieldInformation);
        Assert.Equal("extra", fieldInformation.ExtensionName);
        Assert.Null(fieldInformation.FieldName);

        result = extensionFieldInformationManager.TryResolveExtensionFieldInformation("extra", "field_name", out fieldInformation);

        Assert.Equal(4, extensionFieldInformationManager.CountOfCachedExtensionFields);

        Assert.True(result);
        Assert.NotNull(fieldInformation);
        Assert.Equal("extra", fieldInformation.ExtensionName);
        Assert.Equal("field_name", fieldInformation.FieldName);

        result = extensionFieldInformationManager.TryResolveExtensionFieldInformation("extra", "field_name", out fieldInformation);

        Assert.Equal(4, extensionFieldInformationManager.CountOfCachedExtensionFields);

        Assert.True(result);
        Assert.NotNull(fieldInformation);
    }

    [Fact]
    public void InvalidFieldNamesIgnoredTest()
    {
        var extensionFieldInformationManager = new ExtensionFieldInformationManager();

        Assert.False(extensionFieldInformationManager.TryResolveExtensionFieldInformation("ext.", out _));

        Assert.Equal(1, extensionFieldInformationManager.CountOfCachedExtensionFields);

        Assert.False(extensionFieldInformationManager.TryResolveExtensionFieldInformation("EXT.", out _));

        Assert.Equal(1, extensionFieldInformationManager.CountOfCachedExtensionFields);

        Assert.False(extensionFieldInformationManager.TryResolveExtensionFieldInformation("ext..", out _));
        Assert.False(extensionFieldInformationManager.TryResolveExtensionFieldInformation("ext. .", out _));
        Assert.False(extensionFieldInformationManager.TryResolveExtensionFieldInformation("ext..field", out _));
        Assert.False(extensionFieldInformationManager.TryResolveExtensionFieldInformation("ext.something.", out _));
        Assert.False(extensionFieldInformationManager.TryResolveExtensionFieldInformation("ext.SOMETHING.", out _));
        Assert.False(extensionFieldInformationManager.TryResolveExtensionFieldInformation("ext.something. ", out _));

        Assert.Equal(6, extensionFieldInformationManager.CountOfCachedExtensionFields);
    }

    [Fact]
    public void FieldInformationCacheLimitTest()
    {
        var extensionFieldInformationManager = new ExtensionFieldInformationManager();

        for (int i = 0; i < ExtensionFieldInformationManager.MaxNumberOfCachedFieldInformations + 128; i++)
        {
            var fieldName = $"fieldName{i}";

            var result = extensionFieldInformationManager.TryResolveExtensionFieldInformation($"ext.something.{fieldName}", out var fieldInformation);

            Assert.True(result);
            Assert.NotNull(fieldInformation);
            Assert.Equal("something", fieldInformation.ExtensionName);
            Assert.Equal(fieldName, fieldInformation.FieldName);
        }

        Assert.Equal(ExtensionFieldInformationManager.MaxNumberOfCachedFieldInformations, extensionFieldInformationManager.CountOfCachedExtensionFields);
    }

    [Fact]
    public void ExtensionFieldInformationCacheKeyTests()
    {
        var key1 = new ExtensionFieldInformationManager.ExtensionFieldInformationCacheKey()
        {
            FullFieldName = "EXT.field",
            ExtensionName = "EXT",
            FieldName = "field",
        };

        var key2 = new ExtensionFieldInformationManager.ExtensionFieldInformationCacheKey()
        {
            FullFieldName = "ext.FIELD",
            ExtensionName = "ext",
            FieldName = "FIELD",
        };

        var key3 = new ExtensionFieldInformationManager.ExtensionFieldInformationCacheKey()
        {
            FullFieldName = "ext2._",
            ExtensionName = "ext2",
            FieldName = "_",
        };

        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
        Assert.NotEqual(key1.GetHashCode(), key3.GetHashCode());
        Assert.Equal(key1, key2);
        Assert.NotEqual(key1, key3);

        Assert.True(key1 == key2);
        Assert.False(key1 != key2);

        Assert.False(key1 == key3);
        Assert.True(key1 != key3);
    }
}
