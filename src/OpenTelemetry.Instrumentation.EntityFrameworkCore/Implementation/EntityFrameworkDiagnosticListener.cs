// <copyright file="EntityFrameworkDiagnosticListener.cs" company="OpenTelemetry Authors">
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
using System.Data;
using System.Diagnostics;
using OpenTelemetry.Instrumentation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Implementation
{
    internal sealed class EntityFrameworkDiagnosticListener : ListenerHandler
    {
        internal const string DiagnosticSourceName = "Microsoft.EntityFrameworkCore";

        internal const string ActivitySourceName = "OpenTelemetry.EntityFrameworkCore";
        internal const string ActivityName = ActivitySourceName + ".Execute";

        internal const string EntityFrameworkCoreCommandCreated = "Microsoft.EntityFrameworkCore.Database.Command.CommandCreated";
        internal const string EntityFrameworkCoreCommandExecuting = "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuting";
        internal const string EntityFrameworkCoreCommandExecuted = "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted";
        internal const string EntityFrameworkCoreCommandError = "Microsoft.EntityFrameworkCore.Database.Command.CommandError";

        internal const string AttributePeerService = "peer.service";
        internal const string AttributeDbSystem = "db.system";
        internal const string AttributeDbName = "db.name";
        internal const string AttributeDbStatement = "db.statement";

        private static readonly Version Version = typeof(EntityFrameworkDiagnosticListener).Assembly.GetName().Version;
#pragma warning disable SA1202 // Elements should be ordered by access <- In this case, Version MUST come before SqlClientActivitySource otherwise null ref exception is thrown.
        internal static readonly ActivitySource SqlClientActivitySource = new ActivitySource(ActivitySourceName, Version.ToString());
#pragma warning restore SA1202 // Elements should be ordered by access

        private readonly PropertyFetcher<object> commandFetcher = new PropertyFetcher<object>("Command");
        private readonly PropertyFetcher<object> connectionFetcher = new PropertyFetcher<object>("Connection");
        private readonly PropertyFetcher<object> dbContextFetcher = new PropertyFetcher<object>("Context");
        private readonly PropertyFetcher<object> dbContextDatabaseFetcher = new PropertyFetcher<object>("Database");
        private readonly PropertyFetcher<string> providerNameFetcher = new PropertyFetcher<string>("ProviderName");
        private readonly PropertyFetcher<object> dataSourceFetcher = new PropertyFetcher<object>("DataSource");
        private readonly PropertyFetcher<object> databaseFetcher = new PropertyFetcher<object>("Database");
        private readonly PropertyFetcher<CommandType> commandTypeFetcher = new PropertyFetcher<CommandType>("CommandType");
        private readonly PropertyFetcher<string> commandTextFetcher = new PropertyFetcher<string>("CommandText");
        private readonly PropertyFetcher<Exception> exceptionFetcher = new PropertyFetcher<Exception>("Exception");

        private readonly EntityFrameworkInstrumentationOptions options;

        public EntityFrameworkDiagnosticListener(string sourceName, EntityFrameworkInstrumentationOptions options)
            : base(sourceName)
        {
            this.options = options ?? new EntityFrameworkInstrumentationOptions();
        }

        public override bool SupportsNullActivity => true;

        public override void OnCustom(string name, Activity activity, object payload)
        {
            switch (name)
            {
                case EntityFrameworkCoreCommandCreated:
                    {
                        activity = SqlClientActivitySource.StartActivity(ActivityName, ActivityKind.Client);
                        if (activity == null)
                        {
                            // There is no listener or it decided not to sample the current request.
                            return;
                        }

                        var command = this.commandFetcher.Fetch(payload);
                        if (command == null)
                        {
                            EntityFrameworkInstrumentationEventSource.Log.NullPayload(nameof(EntityFrameworkDiagnosticListener), name);
                            activity.Stop();
                            return;
                        }

                        var connection = this.connectionFetcher.Fetch(command);
                        var database = (string)this.databaseFetcher.Fetch(connection);

                        activity.DisplayName = database;

                        if (activity.IsAllDataRequested)
                        {
                            var dbContext = this.dbContextFetcher.Fetch(payload);
                            var dbContextDatabase = this.dbContextDatabaseFetcher.Fetch(dbContext);
                            var providerName = this.providerNameFetcher.Fetch(dbContextDatabase);

                            switch (providerName)
                            {
                                case "Microsoft.EntityFrameworkCore.SqlServer":
                                    activity.AddTag(AttributeDbSystem, "mssql");
                                    break;
                                case "Microsoft.EntityFrameworkCore.Cosmos":
                                    activity.AddTag(AttributeDbSystem, "cosmosdb");
                                    break;
                                case "Microsoft.EntityFrameworkCore.Sqlite":
                                    activity.AddTag(AttributeDbSystem, "sqlite");
                                    break;
                                case "MySql.Data.EntityFrameworkCore":
                                case "Pomelo.EntityFrameworkCore.MySql":
                                    activity.AddTag(AttributeDbSystem, "mysql");
                                    break;
                                case "Npgsql.EntityFrameworkCore.PostgreSQL":
                                    activity.AddTag(AttributeDbSystem, "postgresql");
                                    break;
                                case "Oracle.EntityFrameworkCore":
                                    activity.AddTag(AttributeDbSystem, "oracle");
                                    break;
                                default:
                                    activity.AddTag(AttributeDbSystem, "other_sql");
                                    activity.AddTag("ef.provider", providerName);
                                    break;
                            }

                            var dataSource = (string)this.dataSourceFetcher.Fetch(connection);

                            activity.AddTag(AttributeDbName, database);
                            if (!string.IsNullOrEmpty(dataSource))
                            {
                                activity.AddTag(AttributePeerService, dataSource);
                            }
                        }
                    }

                    break;

                case EntityFrameworkCoreCommandExecuting:
                    {
                        if (activity == null)
                        {
                            EntityFrameworkInstrumentationEventSource.Log.NullActivity(name);
                            return;
                        }

                        if (activity.Source != SqlClientActivitySource)
                        {
                            return;
                        }

                        if (activity.IsAllDataRequested)
                        {
                            var command = this.commandFetcher.Fetch(payload);

                            if (this.commandTypeFetcher.Fetch(command) is CommandType commandType)
                            {
                                var commandText = this.commandTextFetcher.Fetch(command);
                                switch (commandType)
                                {
                                    case CommandType.StoredProcedure:
                                        activity.AddTag(SpanAttributeConstants.DatabaseStatementTypeKey, nameof(CommandType.StoredProcedure));
                                        if (this.options.SetDbStatementForStoredProcedure)
                                        {
                                            activity.AddTag(AttributeDbStatement, commandText);
                                        }

                                        break;

                                    case CommandType.Text:
                                        activity.AddTag(SpanAttributeConstants.DatabaseStatementTypeKey, nameof(CommandType.Text));
                                        if (this.options.SetDbStatementForText)
                                        {
                                            activity.AddTag(AttributeDbStatement, commandText);
                                        }

                                        break;

                                    case CommandType.TableDirect:
                                        activity.AddTag(SpanAttributeConstants.DatabaseStatementTypeKey, nameof(CommandType.TableDirect));
                                        break;
                                }
                            }
                        }
                    }

                    break;

                case EntityFrameworkCoreCommandExecuted:
                    {
                        if (activity == null)
                        {
                            EntityFrameworkInstrumentationEventSource.Log.NullActivity(name);
                            return;
                        }

                        if (activity.Source != SqlClientActivitySource)
                        {
                            return;
                        }

                        activity.Stop();
                    }

                    break;

                case EntityFrameworkCoreCommandError:
                    {
                        if (activity == null)
                        {
                            EntityFrameworkInstrumentationEventSource.Log.NullActivity(name);
                            return;
                        }

                        if (activity.Source != SqlClientActivitySource)
                        {
                            return;
                        }

                        try
                        {
                            if (activity.IsAllDataRequested)
                            {
                                if (this.exceptionFetcher.Fetch(payload) is Exception exception)
                                {
                                    activity.SetStatus(Status.Error.WithDescription(exception.Message));
                                }
                                else
                                {
                                    EntityFrameworkInstrumentationEventSource.Log.NullPayload(nameof(EntityFrameworkDiagnosticListener), name);
                                }
                            }
                        }
                        finally
                        {
                            activity.Stop();
                        }
                    }

                    break;
            }
        }
    }
}
