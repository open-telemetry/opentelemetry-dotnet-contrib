// <copyright file="ConnectionStringBuilderTests.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class ConnectionStringBuilderTests
{
    [Theory]
    [InlineData("key1=value1",                    TransportProtocol.Unspecified)]
    [InlineData("etwSession=OpenTelemetry",       TransportProtocol.Unspecified)]
    [InlineData("monitoringAccount=TestAccount",  TransportProtocol.Unspecified)]
    [InlineData("endpoint=tcp://localhost:33333", TransportProtocol.Unspecified)]
    [InlineData("TimeoutMilliseconds=10000",      TransportProtocol.Unspecified, null, null, null, null, null, null, 10000)]
    [InlineData("Account=TestAccount",            TransportProtocol.Unspecified, null, null, null, null, null, "TestAccount")]
    [InlineData("Account=value1;Account=VALUE2",  TransportProtocol.Unspecified, null, null, null, null, null, "VALUE2")]
    [InlineData("Namespace=TestNamespace",        TransportProtocol.Unspecified, null, null, null, null, null, null, null, "TestNamespace")]
    [InlineData("EtwSession=OpenTelemetry",                                TransportProtocol.Etw, null, null, null, "OpenTelemetry")]
    [InlineData("Endpoint=tcp://localhost:33333;EtwSession=OpenTelemetry", TransportProtocol.Etw, "tcp://localhost:33333", "localhost", 33333, "OpenTelemetry")]
    [InlineData("Endpoint=udp://localhost:11013", TransportProtocol.Udp, "udp://localhost:11013", "localhost", 11013)]
    [InlineData("Endpoint=UDP://localhost:11111", TransportProtocol.Udp, "UDP://localhost:11111", "localhost", 11111)]
    [InlineData("Endpoint=udp://localhost",       TransportProtocol.Udp, "udp://localhost", "localhost")]
    [InlineData("Endpoint=tcp://localhost:11013", TransportProtocol.Tcp, "tcp://localhost:11013", "localhost", 11013)]
    [InlineData("Endpoint=TCP://localhost:11111", TransportProtocol.Tcp, "TCP://localhost:11111", "localhost", 11111)]
    [InlineData("Endpoint=tcp://localhost",       TransportProtocol.Tcp, "tcp://localhost", "localhost")]
    [InlineData("Endpoint=unix:/var/run/default_fluent.socket",   TransportProtocol.Unix, "unix:/var/run/default_fluent.socket", null, null, null, "/var/run/default_fluent.socket", null, UnixDomainSocketDataTransport.DefaultTimeoutMilliseconds)]
    [InlineData("Endpoint=unix:///var/run/default_fluent.socket", TransportProtocol.Unix, "unix:///var/run/default_fluent.socket", null, null, null, "/var/run/default_fluent.socket")]
    [InlineData("Endpoint=unix:/var/run/default_fluent.socket",   TransportProtocol.Unix, "unix:/var/run/default_fluent.socket", null, null, null, "/var/run/default_fluent.socket")]
    internal void Constructor(
        string str,
        TransportProtocol protocol,
        string Endpoint = null,
        string Host = null,
        int? Port = null,
        string EtwSession = null,
        string udsPath = null,
        string Account = null,
        int? TimeoutMilliseconds = null,
        string Namespace = null)
    {
        ConnectionStringBuilder builder = new(str);

        Assert.Equal(protocol, builder.Protocol);

        if (Endpoint != null)
        {
            Assert.Equal(Endpoint, builder.Endpoint);
        }

        if (Host != null)
        {
            Assert.Equal(Host, builder.Host);
        }

        if (Port != null)
        {
            Assert.Equal(Port, builder.Port);
        }

        if (EtwSession != null)
        {
            Assert.Equal(EtwSession, builder.EtwSession);
        }

        if (udsPath != null)
        {
            Assert.Equal(udsPath, builder.ParseUnixDomainSocketPath());
        }

        if (TimeoutMilliseconds != null)
        {
            Assert.Equal(TimeoutMilliseconds, builder.TimeoutMilliseconds);
        }

        if (Account != null)
        {
            Assert.Equal(Account, builder.Account);
        }

        if (Namespace != null)
        {
            Assert.Equal(Namespace, builder.Namespace);
        }
    }

    // Constructor
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("=value")]
    [InlineData("=value1;key2=value2")]
    [InlineData("key1=value1;=value2")]
    [InlineData("key=")]
    [InlineData("key1=;key2=value2")]
    [InlineData("key1=value1;key2=")]
    [InlineData("key;value")]
    [InlineData("key==value")]
    public void ConstructorThrows(string connectionString)
    {
        Assert.ThrowsAny<Exception>(() => _ = new ConnectionStringBuilder(connectionString));
    }

    // Protocol
    [Theory]
    [InlineData("Endpoint=udp://:11111")]
    [InlineData("Endpoint=tpc://:11111")]
    [InlineData("Endpoint=foo://localhost:11013")]
    public void ProtocolThrows(string connectionString)
    {
        ConnectionStringBuilder builder = new(connectionString);
        Assert.Throws<ArgumentException>(() => _ = builder.Protocol);
    }

    // Endpoint
    [Theory]
    [InlineData("endpoint=tcp://localhost:33333")]
    public void EndpointThrows(string connectionString)
    {
        ConnectionStringBuilder builder = new(connectionString);
        Assert.Throws<ArgumentException>(() => _ = builder.Endpoint);
    }

    // Host
    [Theory]
    [InlineData("EtwSession=OpenTelemetry")]
    [InlineData("endpoint=tcp://localhost:33333")]
    [InlineData("Endpoint=tpc://:11111")]
    public void HostThrows(string connectionString)
    {
        ConnectionStringBuilder builder = new(connectionString);
        Assert.Throws<ArgumentException>(() => _ = builder.Host);
    }

    // Port
    [Theory]
    [InlineData("EtwSession=OpenTelemetry")]
    [InlineData("Endpoint=udp://localhost")]
    [InlineData("Endpoint=udp://:11111")]
    [InlineData("endpoint=tcp://localhost:33333")]
    [InlineData("Endpoint=tpc://:11111")]
    public void PortThrows(string connectionString)
    {
        ConnectionStringBuilder builder = new(connectionString);
        Assert.Throws<ArgumentException>(() => _ = builder.Port);
    }

    // EtwSession
    [Theory]
    [InlineData("Endpoint=udp://localhost:11013")]
    [InlineData("etwSession=OpenTelemetry")]
    public void EtwSessionThrows(string connectionString)
    {
        ConnectionStringBuilder builder = new(connectionString);
        Assert.Throws<ArgumentException>(() => _ = builder.EtwSession);
    }

    // ParseUnixDomainSocketPath()
    [Theory]
    [InlineData("Endpoint=unix://:11111")]
    [InlineData("EtwSession=OpenTelemetry")]
    public void ParseUnixDomainSocketPathThrows(string connectionString)
    {
        ConnectionStringBuilder builder = new(connectionString);
        Assert.Throws<ArgumentException>(() => _ = builder.ParseUnixDomainSocketPath());
    }

    // TimeoutMilliseconds
    [Theory]
    [InlineData("TimeoutMilliseconds=0")]
    [InlineData("TimeoutMilliseconds=-1")]
    [InlineData("TimeoutMilliseconds=10.5")]
    [InlineData("TimeoutMilliseconds=abc")]
    public void TimeoutMillisecondsThrows(string connectionString)
    {
        ConnectionStringBuilder builder = new(connectionString);
        Assert.Throws<ArgumentException>(() => _ = builder.TimeoutMilliseconds);
    }

    // Account
    [Theory]
    [InlineData("key1=value1")]
    [InlineData("account=TestAccount")]

    public void NoAccountThrows(string connectionString)
    {
        ConnectionStringBuilder builder = new(connectionString);
        Assert.Throws<ArgumentException>(() => _ = builder.Account);
    }

    // Namspace
    [Theory]
    [InlineData("key1=value1")]
    [InlineData("namespace=TestNamespace")]

    public void NoNamespaceThrows(string connectionString)
    {
        ConnectionStringBuilder builder = new(connectionString);
        Assert.Throws<ArgumentException>(() => _ = builder.Account);
    }

    // Misc.
    [Fact]
    public void CanUpdateTimeoutMilliseconds()
    {
        ConnectionStringBuilder builder = new("TimeoutMilliseconds=10000");
        builder.TimeoutMilliseconds = 6000;
        Assert.Equal(6000, builder.TimeoutMilliseconds);
    }
}
