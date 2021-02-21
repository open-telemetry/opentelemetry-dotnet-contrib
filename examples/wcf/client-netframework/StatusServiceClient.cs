// <copyright file="StatusServiceClient.cs" company="OpenTelemetry Authors">
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

using System.ServiceModel;
using System.Threading.Tasks;

namespace Examples.Wcf.Client
{
    public class StatusServiceClient : ClientBase<IStatusServiceContract>, IStatusServiceContract
    {
        public StatusServiceClient(string name)
            : base(name)
        {
        }

        public Task<StatusResponse> PingAsync(StatusRequest request)
            => this.Channel.PingAsync(request);

        public Task OpenAsync()
        {
            ICommunicationObject communicationObject = this;
            return Task.Factory.FromAsync(communicationObject.BeginOpen, communicationObject.EndOpen, null);
        }

        public Task CloseAsync()
        {
            ICommunicationObject communicationObject = this;
            return Task.Factory.FromAsync(communicationObject.BeginClose, communicationObject.EndClose, null);
        }
    }
}
