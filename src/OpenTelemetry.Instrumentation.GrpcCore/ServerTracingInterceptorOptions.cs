// <copyright file="ServerTracingInterceptorOptions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Instrumentation.GrpcCore;

/// <summary>
/// Options for the ServerTracingInterceptor.
/// </summary>
public class ServerTracingInterceptorOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether or not to record individual message events.
    /// </summary>
    public bool RecordMessageEvents { get; set; }

    /// <summary>
    /// Gets the propagator.
    /// </summary>
    public TextMapPropagator Propagator { get; internal set; } = Propagators.DefaultTextMapPropagator;

    /// <summary>
    /// Gets or sets a custom identfier used during unit testing.
    /// </summary>
    internal Guid ActivityIdentifierValue { get; set; }
}
