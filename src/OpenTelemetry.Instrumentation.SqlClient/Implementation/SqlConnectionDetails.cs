// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.SqlClient.Implementation;

internal sealed class SqlConnectionDetails
{
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
    private static readonly Regex DataSourceRegex = new("^(.*\\s*:\\s*\\\\{0,2})?(.*?)\\s*(?:[\\\\,]|$)\\s*(.*?)\\s*(?:,|$)\\s*(.*)$", RegexOptions.Compiled);

    /// <summary>
    /// In a Data Source string like "np:\\serverName\pipe\MSSQL$instanceName\pipeName" match the
    /// "pipe\MSSQL$instanceName" segment to extract instanceName if it is available.
    /// </summary>
    /// <see>
    /// <a href="https://docs.microsoft.com/previous-versions/sql/sql-server-2016/ms189307(v=sql.130)"/>
    /// </see>
    private static readonly Regex NamedPipeRegex = new("pipe\\\\MSSQL\\$(.*?)\\\\", RegexOptions.Compiled);

    private static readonly ConcurrentDictionary<string, SqlConnectionDetails> ConnectionDetailCache = new(StringComparer.OrdinalIgnoreCase);

    public string? ServerHostName { get; set; }

    public string? ServerIpAddress { get; set; }

    public string? InstanceName { get; set; }

    public string? Port { get; set; }

    internal static SqlConnectionDetails ParseDataSource(string dataSource)
    {
        Match match = DataSourceRegex.Match(dataSource);

        string? serverHostName = match.Groups[2].Value;
        string? serverIpAddress = null;

        string? instanceName;

        var uriHostNameType = Uri.CheckHostName(serverHostName);
        if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
        {
            serverIpAddress = serverHostName;
            serverHostName = null;
        }

        string maybeProtocol = match.Groups[1].Value;
        bool isNamedPipe = maybeProtocol.Length > 0 &&
                           maybeProtocol.StartsWith("np", StringComparison.OrdinalIgnoreCase);

        if (isNamedPipe)
        {
            string pipeName = match.Groups[3].Value;
            if (pipeName.Length > 0)
            {
                var namedInstancePipeMatch = NamedPipeRegex.Match(pipeName);
                if (namedInstancePipeMatch.Success)
                {
                    instanceName = namedInstancePipeMatch.Groups[1].Value;
                    return new SqlConnectionDetails
                    {
                        ServerHostName = serverHostName,
                        ServerIpAddress = serverIpAddress,
                        InstanceName = instanceName,
                        Port = null,
                    };
                }
            }

            return new SqlConnectionDetails
            {
                ServerHostName = serverHostName,
                ServerIpAddress = serverIpAddress,
                InstanceName = null,
                Port = null,
            };
        }

        string? port;
        if (match.Groups[4].Length > 0)
        {
            instanceName = match.Groups[3].Value;
            port = match.Groups[4].Value;
            if (port == "1433")
            {
                port = null;
            }
        }
        else if (int.TryParse(match.Groups[3].Value, out int parsedPort))
        {
            port = parsedPort == 1433 ? null : match.Groups[3].Value;
            instanceName = null;
        }
        else
        {
            instanceName = match.Groups[3].Value;

            if (string.IsNullOrEmpty(instanceName))
            {
                instanceName = null;
            }

            port = null;
        }

        return new SqlConnectionDetails
        {
            ServerHostName = serverHostName,
            ServerIpAddress = serverIpAddress,
            InstanceName = instanceName,
            Port = port,
        };
    }

    internal static SqlConnectionDetails GetOrAddCached(string dataSource)
    {
        if (!ConnectionDetailCache.TryGetValue(dataSource, out SqlConnectionDetails? connectionDetails))
        {
            connectionDetails = ParseDataSource(dataSource);
            ConnectionDetailCache.TryAdd(dataSource, connectionDetails);
        }

        return connectionDetails;
    }

    internal static void AddConnectionLevelDetailsToActivity(SqlClientTraceInstrumentationOptions options, string dataSource, Activity sqlActivity)
    {
        if (!options.EnableConnectionLevelAttributes)
        {
            sqlActivity.SetTag(SemanticConventions.AttributePeerService, dataSource);
        }
        else
        {
            var connectionDetails = GetOrAddCached(dataSource);

            if (!string.IsNullOrEmpty(connectionDetails.InstanceName))
            {
                sqlActivity.SetTag(SemanticConventions.AttributeDbMsSqlInstanceName, connectionDetails.InstanceName);
            }

            if (!string.IsNullOrEmpty(connectionDetails.ServerHostName))
            {
                sqlActivity.SetTag(SemanticConventions.AttributeServerAddress, connectionDetails.ServerHostName);
            }
            else
            {
                sqlActivity.SetTag(SemanticConventions.AttributeServerSocketAddress, connectionDetails.ServerIpAddress);
            }

            if (!string.IsNullOrEmpty(connectionDetails.Port))
            {
                // TODO: Should we continue to emit this if the default port (1433) is being used?
                sqlActivity.SetTag(SemanticConventions.AttributeServerPort, connectionDetails.Port);
            }
        }
    }
}
