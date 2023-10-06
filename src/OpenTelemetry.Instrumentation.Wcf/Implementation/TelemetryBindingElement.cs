// <copyright file="TelemetryBindingElement.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

/// <summary>
/// A <see cref="BindingElement"/> that can be used to instrument WCF clients.
/// </summary>
internal sealed class TelemetryBindingElement : BindingElement
{
    /// <inheritdoc/>
    public override IChannelFactory<TChannel> BuildChannelFactory<TChannel>(BindingContext context)
    {
        Guard.ThrowIfNull(context);

        return new InstrumentedChannelFactory<TChannel>(context.BuildInnerChannelFactory<TChannel>(), context.Binding);
    }

    /// <inheritdoc/>
    public override bool CanBuildChannelFactory<TChannel>(BindingContext context)
    {
        Guard.ThrowIfNull(context);

        var supportedByInstrumentation =
            typeof(TChannel) == typeof(IRequestChannel) ||
            typeof(TChannel) == typeof(IRequestSessionChannel) ||
            typeof(TChannel) == typeof(IDuplexChannel) ||
            typeof(TChannel) == typeof(IDuplexSessionChannel);

        return supportedByInstrumentation && base.CanBuildChannelFactory<TChannel>(context);
    }

    /// <inheritdoc/>
    public override BindingElement Clone()
    {
        return new TelemetryBindingElement();
    }

    /// <inheritdoc/>
    public override T GetProperty<T>(BindingContext context)
    {
        Guard.ThrowIfNull(context);

        return context.GetInnerProperty<T>();
    }
}
