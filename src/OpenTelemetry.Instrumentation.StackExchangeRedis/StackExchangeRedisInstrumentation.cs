// <copyright file="StackExchangeRedisInstrumentation.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using StackExchange.Redis;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis;

/// <summary>
/// StackExchange.Redis instrumentation.
/// </summary>
public sealed class StackExchangeRedisInstrumentation : IDisposable
{
    private readonly IOptionsMonitor<StackExchangeRedisInstrumentationOptions> options;

    internal StackExchangeRedisInstrumentation(
        IOptionsMonitor<StackExchangeRedisInstrumentationOptions> options)
    {
        this.options = options;
    }

    internal List<StackExchangeRedisConnectionInstrumentation> InstrumentedConnections { get; } = new();

    /// <summary>
    /// Adds an <see cref="IConnectionMultiplexer"/> to the instrumentation.
    /// </summary>
    /// <param name="connection"><see cref="IConnectionMultiplexer"/>.</param>
    /// <returns><see cref="IDisposable"/> to cancel the registration.</returns>
    public IDisposable AddConnection(IConnectionMultiplexer connection)
        => this.AddConnection(Options.DefaultName, connection);

    /// <summary>
    /// Adds an <see cref="IConnectionMultiplexer"/> to the instrumentation.
    /// </summary>
    /// <param name="name">Name to use when retrieving options.</param>
    /// <param name="connection"><see cref="IConnectionMultiplexer"/>.</param>
    /// <returns><see cref="IDisposable"/> to cancel the registration.</returns>
    public IDisposable AddConnection(string name, IConnectionMultiplexer connection)
    {
        Guard.ThrowIfNull(name);
        Guard.ThrowIfNull(connection);

        var options = this.options.Get(name);

        lock (this.InstrumentedConnections)
        {
            var instrumentation = new StackExchangeRedisConnectionInstrumentation(connection, name, options);

            this.InstrumentedConnections.Add(instrumentation);

            return new StackExchangeRedisConnectionInstrumentationRegistration(() =>
            {
                lock (this.InstrumentedConnections)
                {
                    if (this.InstrumentedConnections.Remove(instrumentation))
                    {
                        instrumentation.Dispose();
                    }
                }
            });
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (this.InstrumentedConnections)
        {
            foreach (var instrumentation in this.InstrumentedConnections)
            {
                instrumentation.Dispose();
            }

            this.InstrumentedConnections.Clear();
        }
    }

    private sealed class StackExchangeRedisConnectionInstrumentationRegistration : IDisposable
    {
        private readonly Action disposalAction;

        public StackExchangeRedisConnectionInstrumentationRegistration(
            Action disposalAction)
        {
            this.disposalAction = disposalAction;
        }

        public void Dispose()
        {
            this.disposalAction();
        }
    }
}
