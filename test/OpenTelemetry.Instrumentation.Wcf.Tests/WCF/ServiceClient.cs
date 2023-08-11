// <copyright file="ServiceClient.cs" company="OpenTelemetry Authors">
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
using System.ServiceModel.Channels;
using System.Threading.Tasks;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

public class ServiceClient : ClientBase<IServiceContract>, IServiceContract
{
    public ServiceClient(Binding binding, EndpointAddress remoteAddress)
        : base(binding, remoteAddress)
    {
    }

    public Task<ServiceResponse> ExecuteAsync(ServiceRequest request)
        => this.Channel.ExecuteAsync(request);

    public ServiceResponse ExecuteSynchronous(ServiceRequest request)
        => this.Channel.ExecuteSynchronous(request);

    public Task<ServiceResponse> ExecuteWithEmptyActionNameAsync(ServiceRequest request)
        => this.Channel.ExecuteWithEmptyActionNameAsync(request);

    public void ExecuteWithOneWay(ServiceRequest request)
        => this.Channel.ExecuteWithOneWay(request);

    public Task ErrorAsync()
        => this.Channel.ErrorAsync();

    public void ErrorSynchronous()
        => this.Channel.ErrorSynchronous();
}
