// <copyright file="MySqlDataPatchInstrumentation.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MySql.Data.MySqlClient;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.MySqlData;

internal static class MySqlDataPatchInstrumentation<TCommand>
    where TCommand : DbCommand
{
    private static readonly Harmony Harmony = new("Mysql.Data");

    private static readonly ConcurrentDictionary<TCommand, PatchState> CommandsCache = new();

    private static readonly Func<string, MySqlConnectionStringBuilder?>? ConnStrBuilderFactory = GetMysqlConnStrBuilder();

    private static MySqlDataInstrumentationOptions options = new();

    public static void Initialize(MySqlDataInstrumentationOptions providedOptions)
    {
        options = providedOptions;

        var patchType = typeof(MySqlDataPatchInstrumentation<TCommand>);
        var prefixMethod = AccessTools.DeclaredMethod(patchType, nameof(PatchPrefix));
        var finalizerMethod = AccessTools.DeclaredMethod(patchType, nameof(PatchFinalizer));
        var finalizerAsyncMethod = AccessTools.DeclaredMethod(patchType, nameof(PatchFinalizerAsync));

        // Use HarmonyLib to patch MySqlCommand methods
        foreach (var methodInfo in AccessTools.GetDeclaredMethods(typeof(TCommand)))
        {
            var patchInfo = Harmony.GetPatchInfo(methodInfo);
            if (patchInfo != null && patchInfo.Owners.Contains(Harmony.Id))
            {
                continue;
            }

            try
            {
                switch (methodInfo.Name)
                {
                    case "ExecuteNonQuery" or "ExecuteReader" or "ExecuteScalar" or "ExecuteDbDataReader":
                        Harmony.Patch(methodInfo, prefixMethod, finalizer: finalizerMethod);
                        break;
                    case "ExecuteNonQueryAsync" or "ExecuteReaderAsync" or "ExecuteScalarAsync" or "ExecuteDbDataReaderAsync":
                        // Async methods' return type is Task<T>
                        var m = finalizerAsyncMethod.MakeGenericMethod(methodInfo.ReturnType.GenericTypeArguments[0]);
                        Harmony.Patch(methodInfo, prefixMethod, finalizer: m);
                        break;
                }
            }
            catch (Exception e)
            {
                MySqlDataInstrumentationEventSource.Log.ErrorInitialize($"Failed to patch MySqlCommand method {methodInfo}", e.ToString());
            }
        }
    }

#pragma warning disable SA1313 // ParameterNamesMustBeginWithLowerCaseLetter
    private static void PatchPrefix(TCommand __instance, out PatchState? __state)
#pragma warning restore SA1313 // ParameterNamesMustBeginWithLowerCaseLetter
    {
        __state = null;
        if (CommandsCache.TryGetValue(__instance, out var state))
        {
            // current MySqlCommand is has already been instrumented, don't create new Activity
            // for example: ExecuteReader() -> ExecuteReader(commandBehavior)
            state.Level++;
            __state = state;
        }
        else
        {
            var parent = Activity.Current;
            if (parent is { IsStopped: true } && parent.Source == MySqlActivitySourceHelper.ActivitySource)
            {
                // Activity.Current is already stopped by previous Finalizer
                Activity.Current = parent.Parent;
            }

            // let parentContext to use default here to automatic pickup current Activity as parent when starting Activity
            var activity = MySqlActivitySourceHelper.ActivitySource.StartActivity(
                MySqlActivitySourceHelper.ActivityName,
                ActivityKind.Client,
                parentContext: default,
                MySqlActivitySourceHelper.CreationTags);
            if (activity == null)
            {
                return;
            }

            state = new PatchState();
            if (CommandsCache.TryAdd(__instance, state))
            {
                __state = state;
            }
        }
    }

#pragma warning disable SA1313 // ParameterNamesMustBeginWithLowerCaseLetter
    private static void PatchFinalizer(TCommand __instance, PatchState? __state, Exception? __exception)
#pragma warning restore SA1313 // ParameterNamesMustBeginWithLowerCaseLetter
    {
        if (__state == null)
        {
            return;
        }

        if (__exception != null)
        {
            RecordException(__exception, __state);
        }

        FinishActivity(__instance, __state);
    }

#pragma warning disable SA1313 // ParameterNamesMustBeginWithLowerCaseLetter
    private static void PatchFinalizerAsync<T>(TCommand __instance, ref Task<T> __result, PatchState? __state, Exception? __exception)
#pragma warning restore SA1313 // ParameterNamesMustBeginWithLowerCaseLetter
    {
        if (__state == null)
        {
            return;
        }

        if (__exception != null)
        {
            RecordException(__exception, __state);
            FinishActivity(__instance, __state);
            return;
        }

        // Since we used another async method to stop Activity here, Activity.Current will be the stopped Activity instead of the parent.
        __result = AwaitTask(__result, __instance, __state);
    }

    private static async Task<T> AwaitTask<T>(Task<T> task, TCommand mySqlCommand, PatchState state)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (Exception e) when (RecordException(e, state))
        {
            // should never reached here since RecordException always returns false
            throw;
        }
        finally
        {
            FinishActivity(mySqlCommand, state);
        }
    }

    private static bool RecordException(Exception exception, PatchState state)
    {
        if (options.RecordException && !state.ExceptionRecorded)
        {
            Activity.Current?.RecordException(exception);
            state.ExceptionRecorded = true;
        }

        Activity.Current?.SetStatus(ActivityStatusCode.Error, exception.Message);
        Activity.Current?.SetStatus(Status.Error.WithDescription(exception.Message));
        return false;
    }

    private static void FinishActivity(TCommand mySqlCommand, PatchState state)
    {
        var activity = Activity.Current;
        if (activity == null)
        {
            return;
        }

        if (state.Level > 0)
        {
            state.Level--;
            return;
        }

        try
        {
            if (activity.IsAllDataRequested)
            {
                if (options.SetDbStatement)
                {
                    activity.SetTag(SemanticConventions.AttributeDbStatement, mySqlCommand.CommandText);
                }

                // the value `300` is from Mysql.Data.TracingDriver.SendQueryAsync
                activity.DisplayName = mySqlCommand.CommandText.Length > 300
                    ? mySqlCommand.CommandText.Substring(0, 300)
                    : mySqlCommand.CommandText;
                activity.SetTag(SemanticConventions.AttributeDbName, mySqlCommand.Connection.Database);

                AddConnectionLevelDetailsToActivity(mySqlCommand.Connection, activity);
            }
        }
        finally
        {
            activity.Stop();
            CommandsCache.TryRemove(mySqlCommand, out _);
        }
    }

    private static Func<string, MySqlConnectionStringBuilder?>? GetMysqlConnStrBuilder()
    {
        var constructor = AccessTools.GetDeclaredConstructors(typeof(MySqlConnectionStringBuilder)).FirstOrDefault(c =>
        {
            var parameters = c.GetParameters();
            if (parameters.Length == 0)
            {
                return false;
            }

            if (parameters[0].ParameterType == typeof(string))
            {
                return true;
            }

            return false;
        });
        if (constructor == null)
        {
            return null;
        }

        var parameterInfos = constructor.GetParameters().Skip(1);

        return connStr =>
        {
            var parameters = new List<object> { connStr };
            foreach (var parameterInfo in parameterInfos)
            {
                parameters.Add(parameterInfo.DefaultValue);
            }

            return constructor.Invoke(parameters.ToArray()) as MySqlConnectionStringBuilder;
        };
    }

    private static void AddConnectionLevelDetailsToActivity(DbConnection connection, Activity sqlActivity)
    {
        var dataSource = ConnStrBuilderFactory?.Invoke(connection.ConnectionString);
        if (dataSource == null)
        {
            return;
        }

        if (!options.EnableConnectionLevelAttributes)
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
            sqlActivity.SetTag(SemanticConventions.AttributeDbConnectionString, dataSource.GetConnectionString(false));
        }
    }

    private sealed record PatchState
    {
        public int Level { get; set; }

        public bool ExceptionRecorded { get; set; }
    }
}
