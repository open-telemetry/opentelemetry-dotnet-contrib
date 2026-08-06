// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Tests;

public class SqlConnectionDetailsTests
{
    [Theory]
    [InlineData("localhost", "localhost", null, null, null)]
    [InlineData("127.0.0.1", null, "127.0.0.1", null, null)]
    [InlineData("[::1]", null, "[::1]", null, null)]
    [InlineData("127.0.0.1,1433", null, "127.0.0.1", null, null)]
    [InlineData("127.0.0.1, 1818", null, "127.0.0.1", null, 1818)]
    [InlineData("[::1],1818", null, "[::1]", null, 1818)]
    [InlineData("127.0.0.1  \\  instanceName", null, "127.0.0.1", "instanceName", null)]
    [InlineData("127.0.0.1\\instanceName, 1818", null, "127.0.0.1", "instanceName", 1818)]
    [InlineData("tcp:127.0.0.1\\instanceName, 1818", null, "127.0.0.1", "instanceName", 1818)]
    [InlineData("tcp:localhost", "localhost", null, null, null)]
    [InlineData("tcp:[::1]", null, "[::1]", null, null)]
    [InlineData("tcp : localhost", "localhost", null, null, null)]
    [InlineData("tcp://some.domain.local:5432", "some.domain.local", null, null, 5432)]
    [InlineData("tcp://some.domain.local", "some.domain.local", null, null, null)]
    [InlineData("tcp://[::1]:5432", null, "[::1]", null, 5432)]
    [InlineData("tcp://[::1]", null, "[::1]", null, null)]
    [InlineData("np : localhost", "localhost", null, null, null)]
    [InlineData("lpc:localhost", "localhost", null, null, null)]
    [InlineData("np:\\\\localhost\\pipe\\sql\\query", "localhost", null, null, null)]
    [InlineData("np : \\\\localhost\\pipe\\sql\\query", "localhost", null, null, null)]
    [InlineData("np:\\\\localhost\\pipe\\MSSQL$instanceName\\sql\\query", "localhost", null, "instanceName", null)]
    public void ParseFromDataSourceTests(
        string dataSource,
        string? expectedServerHostName,
        string? expectedServerIpAddress,
        string? expectedInstanceName,
        int? expectedPort)
    {
        var sqlConnectionDetails = SqlConnectionDetails.ParseFromDataSource(dataSource);

        Assert.NotNull(sqlConnectionDetails);
        Assert.Equal(expectedServerHostName, sqlConnectionDetails.ServerHostName);
        Assert.Equal(expectedServerIpAddress, sqlConnectionDetails.ServerIpAddress);
        Assert.Equal(expectedInstanceName, sqlConnectionDetails.InstanceName);
        Assert.Equal(expectedPort, sqlConnectionDetails.Port);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("localhost", "localhost")]
    [InlineData("127.0.0.1,1433", "127.0.0.1")]
    [InlineData("127.0.0.1, 1818", "127.0.0.1:1818")]
    [InlineData("[::1],1818", "[::1]:1818")]
    [InlineData("tcp://some.domain.local:5432", "some.domain.local:5432")]
    public void ParseFromDataSourceDerivesTheServerAddressAndPort(string dataSource, string? expectedServerAddressAndPort)
    {
        var sqlConnectionDetails = SqlConnectionDetails.ParseFromDataSource(dataSource);

        Assert.Equal(expectedServerAddressAndPort, sqlConnectionDetails.ServerAddressAndPort);
    }

    [Fact]
    public void ParseFromDataSourceBoxesThePortOnce()
    {
        var sqlConnectionDetails = SqlConnectionDetails.ParseFromDataSource("boxed-port.localhost, 1818");

        Assert.Equal(1818, sqlConnectionDetails.Port);
        Assert.Equal(1818, sqlConnectionDetails.BoxedPort);

        // The port is boxed when the data source is parsed, not every time it is used.
        Assert.Same(sqlConnectionDetails.BoxedPort, sqlConnectionDetails.BoxedPort);
    }

    [Fact]
    public void ParseFromDataSourceDoesNotBoxADefaultPort()
    {
        var sqlConnectionDetails = SqlConnectionDetails.ParseFromDataSource("default-port.localhost,1433");

        Assert.Null(sqlConnectionDetails.Port);
        Assert.Null(sqlConnectionDetails.BoxedPort);
    }

    [Fact]
    public void GetDbNamespaceReturnsTheDatabaseNameWhenThereIsNoInstanceName()
    {
        const string DatabaseName = "main";

        var sqlConnectionDetails = SqlConnectionDetails.ParseFromDataSource("no-instance.localhost");

        Assert.Null(sqlConnectionDetails.InstanceName);

        // With no instance name to qualify it, the database name is already the namespace.
        Assert.Same(DatabaseName, sqlConnectionDetails.GetDbNamespace(DatabaseName));
    }

    [Fact]
    public void GetDbNamespaceQualifiesTheDatabaseNameWithTheInstanceName()
    {
        var sqlConnectionDetails = SqlConnectionDetails.ParseFromDataSource("with-instance.localhost\\instanceName");

        Assert.Equal("instanceName", sqlConnectionDetails.InstanceName);
        Assert.Equal("instanceName.main", sqlConnectionDetails.GetDbNamespace("main"));
    }

    [Fact]
    public void GetDbNamespaceReusesTheNamespaceForTheSameDatabase()
    {
        var sqlConnectionDetails = SqlConnectionDetails.ParseFromDataSource("reused-instance.localhost\\instanceName");

        var first = sqlConnectionDetails.GetDbNamespace("main");
        var second = sqlConnectionDetails.GetDbNamespace("main");

        Assert.Equal("instanceName.main", first);
        Assert.Same(first, second);
    }

    [Fact]
    public void GetDbNamespaceDerivesTheNamespaceAgainWhenTheDatabaseChanges()
    {
        var sqlConnectionDetails = SqlConnectionDetails.ParseFromDataSource("changed-instance.localhost\\instanceName");

        var main = sqlConnectionDetails.GetDbNamespace("main");
        var tempdb = sqlConnectionDetails.GetDbNamespace("tempdb");

        Assert.Equal("instanceName.main", main);
        Assert.Equal("instanceName.tempdb", tempdb);

        // Only the most recent database is kept, so the first is derived again rather
        // than the data source retaining a namespace for every database it is used with.
        Assert.NotSame(main, sqlConnectionDetails.GetDbNamespace("main"));
    }
}
