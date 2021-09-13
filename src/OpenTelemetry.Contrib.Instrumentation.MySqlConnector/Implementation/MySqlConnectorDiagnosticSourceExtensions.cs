// <copyright file="MySqlConnectorDiagnosticSourceExtensions.cs" company="OpenTelemetry Authors">
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
using MySqlConnector;

namespace OpenTelemetry.Contrib.Instrumentation.MySqlConnector.Implementation
{
    internal static class MySqlConnectorDiagnosticSourceExtensions
    {
        private const string Prefix = "MySqlConnector.";
        private const string ExecuteCommandStartName = Prefix + nameof(ExecuteCommandStart);
        private const string ExecuteCommandExceptionName = Prefix + nameof(ExecuteCommandException);
        private const string ExecuteCommandStopName = Prefix + nameof(ExecuteCommandStop);

        public static Guid ExecuteCommandStart(this DiagnosticSource @this, MySqlCommand command)
        {
            if (!@this.IsEnabled(ExecuteCommandStartName))
            {
                return Guid.Empty;
            }

            var operationId = Guid.NewGuid();

            @this.Write(ExecuteCommandStartName, new
            {
                OperationId = operationId,
                Command = command,
            });

            return operationId;
        }

        public static bool ExecuteCommandException(this DiagnosticSource @this, Guid operationId, MySqlCommand command, Exception ex)
        {
            if (!@this.IsEnabled(ExecuteCommandExceptionName))
            {
                return false;
            }

            @this.Write(ExecuteCommandExceptionName, new
            {
                OperationId = operationId,
                Command = command,
                Exception = ex,
            });

            return false;
        }

        public static void ExecuteCommandStop(this DiagnosticSource @this, Guid operationId, DbCommand command, object result)
        {
            if (!@this.IsEnabled(ExecuteCommandStopName))
            {
                return;
            }

            @this.Write(ExecuteCommandStopName, new
            {
                OperationId = operationId,
                Command = command,
                Result = result,
            });
        }
    }
}
