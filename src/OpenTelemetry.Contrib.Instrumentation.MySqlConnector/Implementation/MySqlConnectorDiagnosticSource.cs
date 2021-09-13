// <copyright file="MySqlConnectorDiagnosticSource.cs" company="OpenTelemetry Authors">
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
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MySqlConnector;

namespace OpenTelemetry.Contrib.Instrumentation.MySqlConnector.Implementation
{
    internal static class MySqlConnectorDiagnosticSource
    {
        private static readonly DiagnosticSource DiagnosticListener = new DiagnosticListener("MySqlConnector");
        private static readonly Harmony Harmony = new Harmony(nameof(MySqlConnectorDiagnosticSource));

        static MySqlConnectorDiagnosticSource()
        {
            var prefixMethod = typeof(MySqlConnectorDiagnosticSource).GetMethod(nameof(BeginExecuteCommand), BindingFlags.Static | BindingFlags.NonPublic);
            var finalizerMethod = typeof(MySqlConnectorDiagnosticSource).GetMethod(nameof(EndExecuteCommand), BindingFlags.Static | BindingFlags.NonPublic);

            foreach (var method in typeof(MySqlCommand)
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(x => x.Name is nameof(DbCommand.ExecuteNonQueryAsync) or
                    nameof(DbCommand.ExecuteReaderAsync) or nameof(DbCommand.ExecuteScalarAsync)))
            {
                Harmony.Patch(
                    method,
                    new HarmonyMethod(prefixMethod),
                    null,
                    null,
                    new HarmonyMethod(finalizerMethod.MakeGenericMethod(method.ReturnType.GetGenericArguments()[0])),
                    null);
            }
        }

        public static void Start()
        {
        }

#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
        private static void BeginExecuteCommand(MySqlCommand __instance, out Guid __state) =>
#pragma warning restore SA1313 // Parameter names should begin with lower-case letter
            __state = DiagnosticListener.ExecuteCommandStart(__instance);

#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
        private static void EndExecuteCommand<T>(MySqlCommand __instance, ref Task<T> __result, Exception __exception, Guid __state)
#pragma warning restore SA1313 // Parameter names should begin with lower-case letter
        {
            if (__exception != null)
            {
                DiagnosticListener.ExecuteCommandException(__state, __instance, __exception);
            }
            else
            {
                __result = AwaitTask(__result, __state, __instance);
            }
        }

        private static async Task<T> AwaitTask<T>(Task<T> task, Guid operationId, MySqlCommand command)
        {
            object temp = null;
            try
            {
                var result = await task.ConfigureAwait(false);

                temp = result;

                return result;
            }
            catch (Exception ex) when (DiagnosticListener.ExecuteCommandException(operationId, command, ex))
            {
                throw;
            }
            finally
            {
                if (temp != null)
                {
                    DiagnosticListener.ExecuteCommandStop(operationId, command, temp);
                }
            }
        }
    }
}
