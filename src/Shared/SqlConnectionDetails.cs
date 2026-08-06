// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;

namespace OpenTelemetry.Instrumentation;

internal sealed partial class SqlConnectionDetails
{
    /// <summary>
    /// Timeout in milliseconds for regex operations to mitigate potential ReDoS
    /// attacks when the data source string contains unexpected input.
    /// </summary>
    private const int RegexTimeoutMs = 1_000;

    private static readonly ConcurrentDictionary<string, SqlConnectionDetails> ConnectionDetailCache = new(StringComparer.OrdinalIgnoreCase);

    private DbNamespaceEntry? dbNamespace;

    private SqlConnectionDetails()
    {
    }

    public string? ServerHostName { get; private set; }

    public string? ServerIpAddress { get; private set; }

    public string? InstanceName { get; private set; }

    public object? BoxedPort { get; private set; }

    public string? ServerAddressAndPort { get; private set; }

    public int? Port => (int?)this.BoxedPort;

    public static SqlConnectionDetails ParseFromDataSource(string dataSource)
    {
        if (ConnectionDetailCache.TryGetValue(dataSource, out var connectionDetails))
        {
            return connectionDetails;
        }

        try
        {
            var match = DataSourceRegex().Match(dataSource);

            var serverHostName = match.Groups["host"].Value;
            string? serverIpAddress = null;
            string? instanceName = null;
            int? port = null;

            var uriHostNameType = Uri.CheckHostName(serverHostName);
            if (uriHostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
            {
                serverIpAddress = serverHostName;
                serverHostName = null;
            }

            var maybeProtocol = match.Groups["protocol"].Value;
            var isNamedPipe = maybeProtocol.Length > 0 &&
                              maybeProtocol.StartsWith("np", StringComparison.OrdinalIgnoreCase);

            if (isNamedPipe)
            {
                var pipeName = match.Groups["nameOrPort"].Value;
                if (pipeName.Length > 0)
                {
                    var namedInstancePipeMatch = NamedPipeRegex().Match(pipeName);
                    if (namedInstancePipeMatch.Success)
                    {
                        instanceName = namedInstancePipeMatch.Groups["instanceName"].Value;
                    }
                }
            }
            else
            {
                if (match.Groups["port"].Length > 0)
                {
                    instanceName = match.Groups["nameOrPort"].Value;
                    port = int.TryParse(match.Groups["port"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPort)
                        ? parsedPort == 1433 ? null : parsedPort
                        : null;
                }
                else if (int.TryParse(match.Groups["nameOrPort"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPort))
                {
                    instanceName = null;
                    port = parsedPort == 1433 ? null : parsedPort;
                }
                else
                {
                    instanceName = match.Groups["nameOrPort"].Value;
                    if (string.IsNullOrEmpty(instanceName))
                    {
                        instanceName = null;
                    }

                    port = null;
                }
            }

            var serverAddress = serverHostName ?? serverIpAddress;

            connectionDetails = new SqlConnectionDetails
            {
                BoxedPort = port,
                InstanceName = instanceName,
                ServerHostName = serverHostName,
                ServerIpAddress = serverIpAddress,
                ServerAddressAndPort = string.IsNullOrEmpty(serverAddress)
                    ? null
                    : port is { } portNumber ? $"{serverAddress}:{portNumber}" : serverAddress,
            };
        }
        catch (RegexMatchTimeoutException)
        {
            connectionDetails = new SqlConnectionDetails();
        }

        ConnectionDetailCache.TryAdd(dataSource, connectionDetails);
        return connectionDetails;
    }

    /// <summary>
    /// Gets the <c>db.namespace</c> for a database reached through this data source.
    /// </summary>
    /// <param name="databaseName">The database name.</param>
    /// <returns>
    /// <paramref name="databaseName"/> qualified by the instance name, if there is one.
    /// </returns>
    /// <remarks>
    /// Qualifying allocates, so the last result is kept. A data source is almost always used with
    /// a single database, and holding one entry keeps what a data source can retain bounded.
    /// </remarks>
    public string GetDbNamespace(string databaseName)
    {
        if (string.IsNullOrEmpty(this.InstanceName))
        {
            return databaseName;
        }

        var lastDbNamespace = Volatile.Read(ref this.dbNamespace);

        if (lastDbNamespace is not null &&
            string.Equals(lastDbNamespace.DatabaseName, databaseName, StringComparison.Ordinal))
        {
            return lastDbNamespace.DbNamespace;
        }

        var value = $"{this.InstanceName}.{databaseName}";

        Volatile.Write(ref this.dbNamespace, new DbNamespaceEntry(databaseName, value));

        return value;
    }

#if NET
    /*
     * Match...
     *  protocol[ ]:[ ]serverName
     *  serverName
     *  serverName[ ]\[ ]instanceName
     *  serverName[ ],[ ]port
     *  serverName[ ]\[ ]instanceName[ ],[ ]port
     *
     * [ ] can be any number of white-space, SQL allows it for some reason.
     *
     * Optional "protocol" can be "tcp", "lpc" (shared memory), or "np" (named pipes). See:
     *  https://docs.microsoft.com/troubleshoot/sql/connect/use-server-name-parameter-connection-string, and
     *  https://docs.microsoft.com/dotnet/api/system.data.sqlclient.sqlconnection.connectionstring?view=dotnet-plat-ext-5.0
     *
     * In case of named pipes the Data Source string can take form of:
     *  np:serverName\instanceName, or
     *  np:\\serverName\pipe\pipeName, or
     *  np:\\serverName\pipe\MSSQL$instanceName\pipeName - in this case a separate regex (see NamedPipeRegex below)
     *  is used to extract instanceName
     */
    [GeneratedRegex("^(?<protocol>[^:[]*\\s*:\\s*(?:[\\\\/]{0,2})?)?(?<host>\\[[^\\]]+\\]|.*?)\\s*(?:[\\\\,:]|$)\\s*(?<nameOrPort>.*?)\\s*(?:,|$)\\s*(?<port>.*)$", RegexOptions.None, RegexTimeoutMs)]
    private static partial Regex DataSourceRegex();
#else
#pragma warning disable SA1201 // A field should not follow a method
    private static readonly Regex DataSourceRegexField = new("^(?<protocol>[^:[]*\\s*:\\s*(?:[\\\\/]{0,2})?)?(?<host>\\[[^\\]]+\\]|.*?)\\s*(?:[\\\\,:]|$)\\s*(?<nameOrPort>.*?)\\s*(?:,|$)\\s*(?<port>.*)$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(RegexTimeoutMs));
#pragma warning restore SA1201 // A field should not follow a method

    private static Regex DataSourceRegex() => DataSourceRegexField;
#endif

#if NET
    /*
     * In a Data Source string like "np:\\serverName\pipe\MSSQL$instanceName\pipeName" match the
     * "pipe\MSSQL$instanceName" segment to extract instanceName if it is available.
     * https://docs.microsoft.com/previous-versions/sql/sql-server-2016/ms189307(v=sql.130)
     */

    [GeneratedRegex("pipe\\\\MSSQL\\$(?<instanceName>.*?)\\\\", RegexOptions.None, RegexTimeoutMs)]
    private static partial Regex NamedPipeRegex();
#else
#pragma warning disable SA1201 // A field should not follow a method
    private static readonly Regex NamedPipeRegexField = new("pipe\\\\MSSQL\\$(?<instanceName>.*?)\\\\", RegexOptions.Compiled, TimeSpan.FromMilliseconds(RegexTimeoutMs));
#pragma warning restore SA1201 // A field should not follow a method

    private static Regex NamedPipeRegex() => NamedPipeRegexField;
#endif

    private sealed class DbNamespaceEntry(string databaseName, string dbNamespace)
    {
        public string DatabaseName { get; } = databaseName;

        public string DbNamespace { get; } = dbNamespace;
    }
}
