// <copyright file="MySqlDataInstrumentation.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using MySql.Data.MySqlClient;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.MySqlData;

/// <summary>
/// MySql.Data instrumentation.
/// </summary>
internal class MySqlDataInstrumentation : DefaultTraceListener
{
    private readonly ConcurrentDictionary<long, MySqlConnectionStringBuilder> dbConn = new();

    private readonly MySqlDataInstrumentationOptions options;

    private readonly Func<string, MySqlConnectionStringBuilder> builderFactory;

    public MySqlDataInstrumentation(MySqlDataInstrumentationOptions options = null)
    {
        this.options = options ?? new MySqlDataInstrumentationOptions();
        MySqlTrace.Listeners.Clear();
        MySqlTrace.Listeners.Add(this);
        MySqlTrace.Switch.Level = SourceLevels.Information;

        // Mysql.Data removed `MySql.Data.MySqlClient.MySqlTrace.QueryAnalysisEnabled` since 8.0.31 so we need to set this using reflection.
        var queryAnalysisEnabled =
            typeof(MySqlTrace).GetProperty("QueryAnalysisEnabled", BindingFlags.Public | BindingFlags.Static);
        if (queryAnalysisEnabled != null)
        {
            queryAnalysisEnabled.SetValue(null, true);
        }

        // Mysql.Data add optional param `isAnalyzed` to MySqlConnectionStringBuilder constructor since 8.0.32
        var ctor = typeof(MySqlConnectionStringBuilder).GetConstructor(new[] { typeof(string), typeof(bool) });
        if (ctor == null)
        {
            ctor = typeof(MySqlConnectionStringBuilder).GetConstructor(new[] { typeof(string) });
            if (ctor == null)
            {
                MySqlDataInstrumentationEventSource.Log.ErrorInitialize(
                    "Failed to get proper MySqlConnectionStringBuilder constructor, maybe unsupported Mysql.Data version. Connection Level Details will not be available.",
                    string.Empty);
                return;
            }

            var p1 = Expression.Parameter(typeof(string), "connectionString");
            var newExpression = Expression.New(ctor, p1);
            var func = Expression.Lambda<Func<string, MySqlConnectionStringBuilder>>(newExpression, p1).Compile();
            this.builderFactory = s => func(s);
        }
        else
        {
            var p1 = Expression.Parameter(typeof(string));
            var p2 = Expression.Parameter(typeof(bool));
            var newExpression = Expression.New(ctor, p1, p2);
            var func = Expression.Lambda<Func<string, bool, MySqlConnectionStringBuilder>>(newExpression, p1, p2).Compile();
            this.builderFactory = s => func(s, false);
        }
    }

    /// <inheritdoc />
    public override void TraceEvent(
        TraceEventCache eventCache,
        string source,
        TraceEventType eventType,
        int id,
        string format,
        params object[] args)
    {
        try
        {
            switch ((MySqlTraceEventType)id)
            {
                case MySqlTraceEventType.ConnectionOpened:
                    // args: [driverId, connStr, threadId]
                    if (this.builderFactory != null)
                    {
                        var driverId = (long)args[0];
                        var connStr = args[1].ToString();
                        this.dbConn[driverId] = this.builderFactory(connStr);
                    }

                    break;
                case MySqlTraceEventType.ConnectionClosed:
                    break;
                case MySqlTraceEventType.QueryOpened:
                    // args: [driverId, threadId, cmdText]
                    this.BeforeExecuteCommand(this.GetCommand(args[0], args[2]));
                    break;
                case MySqlTraceEventType.ResultOpened:
                    break;
                case MySqlTraceEventType.ResultClosed:
                    break;
                case MySqlTraceEventType.QueryClosed:
                    // args: [driverId]
                    AfterExecuteCommand();
                    break;
                case MySqlTraceEventType.StatementPrepared:
                    break;
                case MySqlTraceEventType.StatementExecuted:
                    break;
                case MySqlTraceEventType.StatementClosed:
                    break;
                case MySqlTraceEventType.NonQuery:
                    break;
                case MySqlTraceEventType.UsageAdvisorWarning:
                    break;
                case MySqlTraceEventType.Warning:
                    break;
                case MySqlTraceEventType.Error:
                    // args: [driverId, exNumber, exMessage]
                    this.ErrorExecuteCommand(GetMySqlErrorException(args[2]));
                    break;
                case MySqlTraceEventType.QueryNormalized:
                    // Should use QueryNormalized event when it exists. Because cmdText in QueryOpened event is incomplete when cmdText.length>300
                    // args: [driverId, threadId, normalized_query]
                    this.OverwriteDbStatement(this.GetCommand(args[0], args[2]));
                    break;
                default:
                    MySqlDataInstrumentationEventSource.Log.UnknownMySqlTraceEventType(id, string.Format(CultureInfo.InvariantCulture, format, args));
                    break;
            }
        }
        catch (Exception e)
        {
            MySqlDataInstrumentationEventSource.Log.ErrorTraceEvent(id, string.Format(CultureInfo.InvariantCulture, format, args), e.ToString());
        }
    }

    private static Exception GetMySqlErrorException(object errorMsg)
    {
#pragma warning disable CA2201 // Do not raise reserved exception types
        return new Exception(errorMsg?.ToString());
#pragma warning restore CA2201 // Do not raise reserved exception types
    }

    private static void AfterExecuteCommand()
    {
        var activity = Activity.Current;
        if (activity == null)
        {
            return;
        }

        if (activity.Source != MySqlActivitySourceHelper.ActivitySource)
        {
            return;
        }

        try
        {
            if (activity.IsAllDataRequested)
            {
                activity.SetStatus(Status.Unset);
            }
        }
        finally
        {
            activity.Stop();
        }
    }

    private void BeforeExecuteCommand(MySqlDataTraceCommand command)
    {
        var activity = MySqlActivitySourceHelper.ActivitySource.StartActivity(
            MySqlActivitySourceHelper.ActivityName,
            ActivityKind.Client,
            Activity.Current?.Context ?? default(ActivityContext),
            MySqlActivitySourceHelper.CreationTags);
        if (activity == null)
        {
            return;
        }

        if (activity.IsAllDataRequested)
        {
            if (this.options.SetDbStatement)
            {
                activity.SetTag(SemanticConventions.AttributeDbStatement, command.SqlText);
            }

            if (command.ConnectionStringBuilder != null)
            {
                activity.DisplayName = command.ConnectionStringBuilder.Database;
                activity.SetTag(SemanticConventions.AttributeDbName, command.ConnectionStringBuilder.Database);

                this.AddConnectionLevelDetailsToActivity(command.ConnectionStringBuilder, activity);
            }
        }
    }

    private void OverwriteDbStatement(MySqlDataTraceCommand command)
    {
        var activity = Activity.Current;
        if (activity == null)
        {
            return;
        }

        if (activity.Source != MySqlActivitySourceHelper.ActivitySource)
        {
            return;
        }

        if (activity.IsAllDataRequested)
        {
            if (this.options.SetDbStatement)
            {
                activity.SetTag(SemanticConventions.AttributeDbStatement, command.SqlText);
            }
        }
    }

    private void ErrorExecuteCommand(Exception exception)
    {
        var activity = Activity.Current;
        if (activity == null)
        {
            return;
        }

        if (activity.Source != MySqlActivitySourceHelper.ActivitySource)
        {
            return;
        }

        try
        {
            if (activity.IsAllDataRequested)
            {
                activity.SetStatus(Status.Error.WithDescription(exception.Message));
                if (this.options.RecordException)
                {
                    activity.RecordException(exception);
                }
            }
        }
        finally
        {
            activity.Stop();
        }
    }

    private MySqlDataTraceCommand GetCommand(object driverIdObj, object cmd)
    {
        var command = new MySqlDataTraceCommand();
        if (this.dbConn.TryGetValue((long)driverIdObj, out var database))
        {
            command.ConnectionStringBuilder = database;
        }

        command.SqlText = cmd == null ? string.Empty : cmd.ToString();
        return command;
    }

    private void AddConnectionLevelDetailsToActivity(MySqlConnectionStringBuilder dataSource, Activity sqlActivity)
    {
        if (!this.options.EnableConnectionLevelAttributes)
        {
            sqlActivity.SetTag(SemanticConventions.AttributePeerService, dataSource.Server);
        }
        else
        {
            var uriHostNameType = Uri.CheckHostName(dataSource.Server);

            if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
            {
                sqlActivity.SetTag(SemanticConventions.AttributeNetPeerIp, dataSource.Server);
            }
            else
            {
                sqlActivity.SetTag(SemanticConventions.AttributeNetPeerName, dataSource.Server);
            }

            sqlActivity.SetTag(SemanticConventions.AttributeNetPeerPort, dataSource.Port);
            sqlActivity.SetTag(SemanticConventions.AttributeDbUser, dataSource.UserID);
        }
    }
}
