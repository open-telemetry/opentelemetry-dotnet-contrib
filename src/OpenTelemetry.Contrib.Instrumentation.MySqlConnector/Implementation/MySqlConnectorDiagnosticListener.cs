// <copyright file="MySqlConnectorDiagnosticListener.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using MySqlConnector;
using OpenTelemetry.Instrumentation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Instrumentation.MySqlConnector.Implementation
{
    internal sealed class MySqlConnectorDiagnosticListener : ListenerHandler
    {
        private readonly MySqlConnectorInstrumentationOptions options;
        private readonly PropertyFetcher<Exception> exceptionFetcher = new PropertyFetcher<Exception>("Exception");
        private readonly PropertyFetcher<MySqlCommand> commandFetcher = new PropertyFetcher<MySqlCommand>("Command");

        public MySqlConnectorDiagnosticListener(string sourceName, MySqlConnectorInstrumentationOptions options)
            : base(sourceName) => this.options = options ?? new MySqlConnectorInstrumentationOptions();

        public override bool SupportsNullActivity => true;

        public override void OnStartActivity(Activity parent, object payload)
        {
            var activity = MySqlActivitySourceHelper.ActivitySource.StartActivity(
                MySqlActivitySourceHelper.ActivityName,
                ActivityKind.Client,
                parent?.Context ?? default,
                MySqlActivitySourceHelper.CreationTags);

            if (activity == null)
            {
                return;
            }

            if (activity.Source != MySqlActivitySourceHelper.ActivitySource)
            {
                return;
            }

            var command = this.commandFetcher.Fetch(payload);

            if (this.options.SetDbStatement)
            {
                activity.SetTag(SemanticConventions.AttributeDbStatement, command.CommandText);
            }

            if (command.Connection == null)
            {
                return;
            }

            var csb = new MySqlConnectionStringBuilder(command.Connection.ConnectionString);

            var database = string.IsNullOrEmpty(csb.Database) ? "default" : csb.Database;
            activity.DisplayName = database;
            activity.SetTag(SemanticConventions.AttributeDbName, database);

            this.AddConnectionLevelDetailsToActivity(csb, activity);
        }

        public override void OnException(Activity activity, object payload)
        {
            if (activity == null || activity.Source != MySqlActivitySourceHelper.ActivitySource)
            {
                return;
            }

            var ex = this.exceptionFetcher.Fetch(payload);

            try
            {
                if (activity.IsAllDataRequested)
                {
                    activity.SetStatus(Status.Error.WithDescription(ex.Message));

                    if (this.options.RecordException)
                    {
                        activity.RecordException(ex);
                    }
                }
            }
            finally
            {
                activity.Stop();
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            if (activity == null || activity.Source != MySqlActivitySourceHelper.ActivitySource)
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

        private void AddConnectionLevelDetailsToActivity(MySqlConnectionStringBuilder dataSource, Activity sqlActivity)
        {
            if (!this.options.EnableConnectionLevelAttributes)
            {
                sqlActivity.SetTag(SemanticConventions.AttributePeerService, dataSource.Server);
            }
            else
            {
                var uriHostNameType = Uri.CheckHostName(dataSource.Server);

                sqlActivity.SetTag(
                    uriHostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
                        ? SemanticConventions.AttributeNetPeerIp
                        : SemanticConventions.AttributeNetPeerName,
                    dataSource.Server);

                sqlActivity.SetTag(SemanticConventions.AttributeNetPeerPort, dataSource.Port);
                sqlActivity.SetTag(SemanticConventions.AttributeDbUser, dataSource.UserID);
            }
        }
    }
}
