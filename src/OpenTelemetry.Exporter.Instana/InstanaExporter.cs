// <copyright file="InstanaExporter.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Instana.Implementation;
using OpenTelemetry.Exporter.Instana.Implementation.Processors;
using OpenTelemetry.Resources;

[assembly: InternalsVisibleTo("OpenTelemetry.Exporter.Instana.Tests, PublicKey=002400000480000094000000060200000024000052534131000400000100010051c1562a090fb0c9f391012a32198b5e5d9a60e9b80fa2d7b434c9e5ccb7259bd606e66f9660676afc6692b8cdc6793d190904551d2103b7b22fa636dcbb8208839785ba402ea08fc00c8f1500ccef28bbf599aa64ffb1e1d5dc1bf3420a3777badfe697856e9d52070a50c3ea5821c80bef17ca3acffa28f89dd413f096f898")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2, PublicKey = 0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7")]

namespace OpenTelemetry.Exporter.Instana
{
    internal class InstanaExporter : BaseExporter<Activity>
    {
        private readonly IActivityProcessor activityProcessor;
        private string name;
        private ISpanSender spanSender = new SpanSender();
        private IInstanaExporterHelper instanaExporterHelper = new InstanaExporterHelper();
        private bool shutdownCalled = false;

        public InstanaExporter(string name = "InstanaExporter", IActivityProcessor activityProcessor = null)
        {
            this.name = name;

            if (activityProcessor != null)
            {
                this.activityProcessor = activityProcessor;
            }
            else
            {
                this.activityProcessor = new DefaultActivityProcessor
                {
                    NextProcessor = new TagsActivityProcessor
                    {
                        NextProcessor = new EventsActivityProcessor
                        {
                            NextProcessor = new ErrorActivityProcessor(),
                        },
                    },
                };
            }
        }

        internal ISpanSender SpanSender
        {
            get { return this.spanSender; }
            set { this.spanSender = value; }
        }

        internal IInstanaExporterHelper InstanaExporterHelper
        {
            get { return this.instanaExporterHelper; }
            set { this.instanaExporterHelper = value; }
        }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            if (this.shutdownCalled)
            {
                return ExportResult.Failure;
            }

            From from = null;
            if (this.instanaExporterHelper.IsWindows())
            {
                from = new From() { E = Process.GetCurrentProcess().Id.ToString() };
            }

            string serviceName = this.ExtractServiceName(ref from);

            foreach (var activity in batch)
            {
                if (activity == null)
                {
                    continue;
                }

                InstanaSpan span = this.ParseActivityAsync(activity, serviceName, from).Result;
                this.spanSender.Enqueue(span);
            }

            return ExportResult.Success;
        }

        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            if (!this.shutdownCalled)
            {
                this.shutdownCalled = true;
                return true;
            }
            else
            {
                return false;
            }
        }

        protected override bool OnForceFlush(int timeoutMilliseconds)
        {
            return base.OnForceFlush(timeoutMilliseconds);
        }

        private string ExtractServiceName(ref From from)
        {
            string serviceName = null;
            string serviceId = null;
            string processId = null;
            string hostId = null;
            var resource = this.instanaExporterHelper.GetParentProviderResource(this);
            if (resource != Resource.Empty && resource.Attributes?.Count() > 0)
            {
                foreach (var resourceAttribute in resource.Attributes)
                {
                    if (resourceAttribute.Key.Equals("service.name", StringComparison.OrdinalIgnoreCase)
                        && resourceAttribute.Value is string servName
                        && !string.IsNullOrEmpty(servName))
                    {
                        serviceName = servName;
                    }

                    if (from == null)
                    {
                        if (resourceAttribute.Key.Equals("service.instance.id", StringComparison.OrdinalIgnoreCase))
                        {
                            serviceId = resourceAttribute.Value.ToString();
                        }
                        else if (resourceAttribute.Key.Equals("process.pid", StringComparison.OrdinalIgnoreCase))
                        {
                            processId = resourceAttribute.Value.ToString();
                        }
                        else if (resourceAttribute.Key.Equals("host.id", StringComparison.OrdinalIgnoreCase))
                        {
                            hostId = resourceAttribute.Value.ToString();
                        }
                    }
                }
            }

            if (from == null)
            {
                from = new From();
                if (!string.IsNullOrEmpty(processId))
                {
                    from.E = processId;
                }
                else if (!string.IsNullOrEmpty(serviceId))
                {
                    from.E = serviceId;
                }

                if (!string.IsNullOrEmpty(hostId))
                {
                    from.H = hostId;
                }
            }

            return serviceName;
        }

        private async Task<InstanaSpan> ParseActivityAsync(Activity activity, string serviceName = null, From from = null)
        {
            InstanaSpan instanaSpan = InstanaSpanFactory.CreateSpan();

            await this.activityProcessor.ProcessAsync(activity, instanaSpan);

            if (!string.IsNullOrEmpty(serviceName))
            {
                instanaSpan.Data.data[InstanaExporterConstants.SERVICE_FIELD] = serviceName;
            }

            instanaSpan.Data.data[InstanaExporterConstants.OPERATION_FIELD] = activity.DisplayName;
            if (!string.IsNullOrEmpty(activity.TraceStateString))
            {
                instanaSpan.Data.data[InstanaExporterConstants.TRACE_STATE_FIELD] = activity.TraceStateString;
            }

            if (from != null)
            {
                instanaSpan.F = from;
            }

            return instanaSpan;
        }
    }
}
