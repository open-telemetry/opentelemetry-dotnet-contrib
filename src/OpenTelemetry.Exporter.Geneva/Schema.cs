// <copyright file="Schema.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Geneva;

internal static class Schema
{
    internal static class V21
    {
        internal static class PartA
        {
            internal const string IKey = ".iKey";
            internal const string Name = ".name";
            internal const string Ver = ".ver";
            internal const string Time = ".time";
            internal const string Cv = ".cv";
            internal const string Epoch = ".epoch";
            internal const string Flags = ".flags";
            internal const string PopSample = ".popSample";
            internal const string SeqNum = ".seqNum";

            internal static class Extensions
            {
                internal static class App
                {
                    internal const string Id = "app.id";
                    internal const string Ver = "app.ver";
                }

                internal static class Cloud
                {
                    internal const string Environment = "cloud.environment";
                    internal const string Location = "cloud.location";
                    internal const string Name = "cloud.name";
                    internal const string DeploymentUnit = "cloud.deploymentUnit";
                    internal const string Role = "cloud.role";
                    internal const string RoleInstance = "cloud.roleInstance";
                    internal const string RoleVer = "cloud.roleVer";
                    internal const string Ver = "cloud.ver";
                }

                internal static class Os
                {
                    internal const string Name = "os.name";
                    internal const string Ver = "os.ver";
                }
            }
        }
    }

    internal static class V40
    {
        internal static class PartA
        {
            internal const string IKey = ".iKey";
            internal const string Name = ".name";
            internal const string Ver = ".ver";
            internal const string Time = ".time";

            internal static class Extensions
            {
                internal static class App
                {
                    internal const string Id = "app.id";
                    internal const string Ver = "app.ver";
                }

                internal static class Cloud
                {
                    internal const string Role = "cloud.role";
                    internal const string RoleInstance = "cloud.roleInstance";
                    internal const string RoleVer = "cloud.roleVer";
                }

                internal static class Os
                {
                    internal const string Name = "os.name";
                    internal const string Ver = "os.ver";
                }

                internal static class Dt
                {
                    internal const string TraceId = "dt.traceId";
                    internal const string SpanId = "dt.spanId";
                }
            }
        }
    }
}
