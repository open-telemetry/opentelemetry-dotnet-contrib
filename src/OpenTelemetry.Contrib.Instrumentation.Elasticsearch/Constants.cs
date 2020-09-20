// <copyright file="Constants.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Contrib.Instrumentation.ElasticsearchClient
{
    internal class Constants
    {
        public const string ExceptionCustomPropertyName = "OTel.Elasticsearch.Exception";
        public const string AttributeDbSystem = "db.system";
        public const string AttributeDbName = "db.name";
        public const string AttributeNetPeerIp = "net.peer.ip";
        public const string AttributeNetPeerName = "net.peer.name";
        public const string AttributeNetPeerPort = "net.peer.port";
        public const string AttributeDbMethod = "db.method";
        public const string AttributeDbUrl = "db.url";
        public const string AttributeDbStatement = "db.statement";
    }
}
