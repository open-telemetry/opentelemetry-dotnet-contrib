// <copyright file="IConnectionRegistry.cs" company="OpenTelemetry Authors">
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

using StackExchange.Redis;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis;

/// <summary>
/// Registers connection for instrumentation purposes.
/// </summary>
public interface IConnectionRegistry
{
    /// <summary>
    /// Registers connection with current instrumentation instance.
    /// </summary>
    /// <param name="connectionMultiplexer">Connection to be tracked by instrumentation.</param>
    void Register(IConnectionMultiplexer connectionMultiplexer);
}
