// <copyright file="OptionsExtensions.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using OpenTelemetry.Contrib.Instrumentation.EventCounters;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Extension methods for the event counters options.
    /// </summary>
    public static class OptionsExtensions
    {
        /// <summary>
        /// Determines whether a provider was already added.
        /// </summary>
        /// <param name="options">The options to check for the provider.</param>
        /// <param name="providerName">Name of the provider to check for existence.</param>
        /// <returns>True if the provider was already added, otherwise false.</returns>
        public static bool HasProvider(this EventCounterListenerOptions options, string providerName) => options.Providers.Any(provider => provider.ProviderName.Equals(providerName, System.StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Adds the metric provider.
        /// </summary>
        /// <param name="options">The options to add the provider to.</param>
        /// <param name="providerName">Name of the provider.</param>
        /// <param name="counterNames">Name of the counters to listen to. Null/empty or all counters.</param>
        /// <returns>The options instance.</returns>
        public static EventCounterListenerOptions AddProvider(this EventCounterListenerOptions options, string providerName, params string[] counterNames)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                throw new ArgumentNullException(nameof(providerName));
            }

            if (options.HasProvider(providerName))
            {
                throw new ArgumentException($"Provider '{providerName}' was already added.", nameof(providerName));
            }

            var provider = new MetricProvider { ProviderName = providerName };
            if (counterNames != null && counterNames.Length > 0)
            {
                provider.CounterNames = counterNames.ToList();
            }

            options.Providers.Add(provider);
            return options;
        }

        /// <summary>
        /// Adds the System.Runtime provider.
        /// </summary>
        /// <param name="options">The options to add the provider to.</param>
        /// <param name="counterNames">Name of the counters to listen to. Null/empty or all counters.</param>
        /// <returns>The options instance.</returns>
        public static EventCounterListenerOptions AddRuntime(this EventCounterListenerOptions options, params string[] counterNames)
        {
            return options.AddProvider(KnownEventSources.SystemRuntime, counterNames);
        }

        /// <summary>
        /// Adds the ASP.NET Core provider.
        /// </summary>
        /// <param name="options">The options to add the provider to.</param>
        /// <param name="counterNames">Name of the counters to listen to. Null/empty or all counters.</param>
        /// <returns>The options instance.</returns>
        public static EventCounterListenerOptions AddAspNetCore(this EventCounterListenerOptions options, params string[] counterNames)
        {
            return options.AddProvider(KnownEventSources.MicrosoftAspNetCoreHosting, counterNames);
        }

        /// <summary>
        /// Adds the Grpc provider.
        /// </summary>
        /// <param name="options">The options to add the provider to.</param>
        /// <param name="counterNames">Name of the counters to listen to. Null/empty or all counters.</param>
        /// <returns>The options instance.</returns>
        public static EventCounterListenerOptions AddGrcpServer(this EventCounterListenerOptions options, params string[] counterNames)
        {
            return options.AddProvider(KnownEventSources.GrpcAspNetCoreServer, counterNames);
        }
    }
}
