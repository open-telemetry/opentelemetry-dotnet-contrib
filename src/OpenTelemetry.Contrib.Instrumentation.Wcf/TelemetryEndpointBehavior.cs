// <copyright file="TelemetryEndpointBehavior.cs" company="OpenTelemetry Authors">
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

using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace OpenTelemetry.Contrib.Instrumentation.Wcf
{
    /// <summary>
    /// An <see cref="IEndpointBehavior"/> implementation whichs add the <see
    /// cref="TelemetryClientMessageInspector"/> to client endpoints and the
    /// <see cref="TelemetryDispatchMessageInspector"/> to service endpoints.
    /// </summary>
    public class TelemetryEndpointBehavior : IEndpointBehavior
    {
        /// <inheritdoc/>
        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        /// <inheritdoc/>
        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.MessageInspectors.Add(new TelemetryClientMessageInspector());
        }

        /// <inheritdoc/>
        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            endpointDispatcher.DispatchRuntime.MessageInspectors.Add(new TelemetryDispatchMessageInspector());
        }

        /// <inheritdoc/>
        public void Validate(ServiceEndpoint endpoint)
        {
        }
    }
}
