// <copyright file="ConnectionStringParserTests.cs" company="OpenTelemetry Authors">
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

public class ConnectionStringParserTests
{
    [Fact]
    public void InvalidConnectionStringTest()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            new ConnectionStringParser("invalid-connection-string");
        });

        Assert.Throws<ArgumentException>(() =>
        {
            new ConnectionStringParser("Key=");
        });

        Assert.Throws<ArgumentException>(() =>
        {
            new ConnectionStringParser("Key1=Value1;Key2=");
        });
    }

    [Fact]
    public void ExtraDataIgnoredInConnectionStringTest()
    {
        var builder = new ConnectionStringParser("Key1=Value1;;;Key2;");

        Assert.Single(builder.ParsedKeyValues);
        Assert.Contains(builder.ParsedKeyValues, kvp => kvp.Key == "Key1" && kvp.Value == "Value1");
    }

    [Fact]
    public void LastOneWinsInConnectionStringTest()
    {
        var builder = new ConnectionStringParser("Key1=Value1;Key1=Value2;");

        Assert.Single(builder.ParsedKeyValues);
        Assert.Contains(builder.ParsedKeyValues, kvp => kvp.Key == "Key1" && kvp.Value == "Value2");
    }
}
