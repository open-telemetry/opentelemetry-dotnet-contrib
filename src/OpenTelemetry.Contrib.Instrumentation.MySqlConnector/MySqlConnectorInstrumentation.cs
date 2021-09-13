// <copyright file="MySqlConnectorInstrumentation.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Contrib.Instrumentation.MySqlConnector.Implementation;
using OpenTelemetry.Instrumentation;

namespace OpenTelemetry.Contrib.Instrumentation.MySqlConnector
{
    internal class MySqlConnectorInstrumentation : IDisposable
    {
        private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

        static MySqlConnectorInstrumentation() => MySqlConnectorDiagnosticSource.Start();

        public MySqlConnectorInstrumentation(MySqlConnectorInstrumentationOptions options = null)
        {
            this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(
               name => new MySqlConnectorDiagnosticListener(name, options),
               listener => listener.Name == "MySqlConnector",
               null);

            this.diagnosticSourceSubscriber.Subscribe();
        }

        public void Dispose() => this.diagnosticSourceSubscriber.Dispose();
    }
}
