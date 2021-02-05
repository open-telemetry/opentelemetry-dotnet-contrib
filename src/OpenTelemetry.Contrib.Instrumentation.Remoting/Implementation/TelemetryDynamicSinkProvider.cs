// <copyright file="TelemetryDynamicSinkProvider.cs" company="OpenTelemetry Authors">
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

using System.Runtime.Remoting.Contexts;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Instrumentation.Remoting.Implementation
{
    /// <summary>
    /// A <see cref="IContributeDynamicSink"/> implementation that returns an instance of
    /// <see cref="TelemetryDynamicSink"/> responsible for instrumenting remoting calls.
    /// </summary>
    internal class TelemetryDynamicSinkProvider : IDynamicProperty, IContributeDynamicSink
    {
        internal const string DynamicPropertyName = "TelemetryDynamicSinkProvider";

        private readonly RemotingInstrumentationOptions options;

        public TelemetryDynamicSinkProvider(RemotingInstrumentationOptions options)
        {
            this.options = options;
        }

        /// <inheritdoc />
        public string Name => DynamicPropertyName;

        /// <summary>
        /// Creates and returns a <see cref="TelemetryDynamicSink"/> to be used for instrumentation.
        /// </summary>
        /// <returns>A new instance of <see cref="TelemetryDynamicSink"/>.</returns>
        public IDynamicMessageSink GetDynamicSink()
        {
            return new TelemetryDynamicSink(this.options);
        }
    }
}
