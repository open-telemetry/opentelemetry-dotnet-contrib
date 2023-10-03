// <copyright file="TelemetryClientMessageInspector.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

/// <summary>
/// An <see cref="IClientMessageInspector"/> implementation which adds telemetry to outgoing requests.
/// </summary>
internal class TelemetryClientMessageInspector : IClientMessageInspector
{
    private readonly IDictionary<string, ActionMetadata> actionMappings;

    internal TelemetryClientMessageInspector(IDictionary<string, ActionMetadata> actionMappings)
    {
        Guard.ThrowIfNull(actionMappings);

        this.actionMappings = actionMappings;
    }

    /// <inheritdoc/>
    public object BeforeSendRequest(ref Message request, IClientChannel channel)
    {
        Guard.ThrowIfNull(request);

        request.Properties.Add(TelemetryContextMessageProperty.Name, new TelemetryContextMessageProperty()
        {
            ActionMappings = this.actionMappings,
        });
        return null;
    }

    /// <inheritdoc/>
    public void AfterReceiveReply(ref Message reply, object correlationState)
    {
    }
}
