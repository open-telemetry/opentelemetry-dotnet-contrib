// <copyright file="Service.netfx.cs" company="OpenTelemetry Authors">
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

#if NETFRAMEWORK
using System.ServiceModel;
using System.Threading.Tasks;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

[ServiceBehavior(
    Namespace = "http://opentelemetry.io/",
    ConcurrencyMode = ConcurrencyMode.Multiple,
    InstanceContextMode = InstanceContextMode.Single,
    UseSynchronizationContext = false,
    Name = "Service")]
public class Service : IServiceContract
{
    public Task ErrorAsync()
    {
        throw new System.Exception();
    }

    public void ErrorSynchronous()
    {
        throw new System.Exception();
    }

    public Task<ServiceResponse> ExecuteAsync(ServiceRequest request)
    {
        return Task.FromResult(
            new ServiceResponse
            {
                Payload = $"RSP: {request.Payload}",
            });
    }

    public ServiceResponse ExecuteSynchronous(ServiceRequest request)
    {
        return new ServiceResponse
            {
                Payload = $"RSP: {request.Payload}",
            };
    }

    public Task<ServiceResponse> ExecuteWithEmptyActionNameAsync(ServiceRequest request)
    {
        return Task.FromResult(
            new ServiceResponse
            {
                Payload = $"RSP: {request.Payload}",
            });
    }

    public void ExecuteWithOneWay(ServiceRequest request)
    {
    }
}

#endif
