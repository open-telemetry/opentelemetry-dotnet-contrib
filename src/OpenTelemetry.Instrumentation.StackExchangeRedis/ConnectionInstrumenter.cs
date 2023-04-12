// <copyright file="ConnectionInstrumenter.cs" company="OpenTelemetry Authors">
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
using StackExchange.Redis;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis;

/// <inheritdoc />
public class ConnectionInstrumenter : IConnectionInstrumenter
{
    private Action<IConnectionMultiplexer> registrationCallback;

    /// <inheritdoc />
    public void Instrument(IConnectionMultiplexer connectionMultiplexer)
    {
        this.registrationCallback?.Invoke(connectionMultiplexer);
    }

    internal void Initialize(Action<IConnectionMultiplexer> callback)
    {
        this.registrationCallback = callback;
    }
}
