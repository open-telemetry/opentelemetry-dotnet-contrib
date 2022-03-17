// <copyright file="WcfInstrumentationConstants.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.Wcf
{
    internal static class WcfInstrumentationConstants
    {
        public const string RpcSystemTag = "rpc.system";
        public const string RpcServiceTag = "rpc.service";
        public const string RpcMethodTag = "rpc.method";
        public const string NetHostNameTag = "net.host.name";
        public const string NetHostPortTag = "net.host.port";
        public const string NetPeerNameTag = "net.peer.name";
        public const string NetPeerPortTag = "net.peer.port";
        public const string SoapMessageVersionTag = "soap.message_version";
        public const string SoapReplyActionTag = "soap.reply_action";
        public const string SoapViaTag = "soap.via";
        public const string WcfChannelSchemeTag = "wcf.channel.scheme";
        public const string WcfChannelPathTag = "wcf.channel.path";

        public const string WcfSystemValue = "wcf";
    }
}
