// <copyright file="TelemetryContractBehaviorAttribute.cs" company="OpenTelemetry Authors">
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
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using OpenTelemetry.Instrumentation.Wcf.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Wcf;

#if NETFRAMEWORK
/// <summary>
/// An <see cref="IContractBehavior"/> <see cref="Attribute"/> to add the
/// <see cref="TelemetryDispatchMessageInspector"/> to service operations,
/// and <see cref="TelemetryClientMessageInspector"/> and
/// TelemetryBindingElement to client operations programmatically.
/// </summary>
#else
/// <summary>
/// An <see cref="IContractBehavior"/> <see cref="Attribute"/> to add the
/// <see cref="TelemetryClientMessageInspector"/> and
/// TelemetryBindingElement to client operations programmatically.
/// </summary>
#endif
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
public sealed class TelemetryContractBehaviorAttribute : Attribute, IContractBehavior
{
    /// <inheritdoc />
    public void AddBindingParameters(ContractDescription contractDescription, ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
    {
    }

    /// <inheritdoc />
    public void ApplyClientBehavior(ContractDescription contractDescription, ServiceEndpoint endpoint, ClientRuntime clientRuntime)
    {
        Guard.ThrowIfNull(clientRuntime);
        Guard.ThrowIfNull(endpoint);
        TelemetryEndpointBehavior.ApplyClientBehaviorToClientRuntime(clientRuntime);
        TelemetryEndpointBehavior.ApplyBindingElementToServiceEndpoint(endpoint);
    }

    /// <inheritdoc />
    public void ApplyDispatchBehavior(ContractDescription contractDescription, ServiceEndpoint endpoint, DispatchRuntime dispatchRuntime)
    {
#if NETFRAMEWORK
        Guard.ThrowIfNull(dispatchRuntime);
        TelemetryEndpointBehavior.ApplyDispatchBehaviorToEndpoint(dispatchRuntime.EndpointDispatcher);
#endif
    }

    /// <inheritdoc />
    public void Validate(ContractDescription contractDescription, ServiceEndpoint endpoint)
    {
    }
}
