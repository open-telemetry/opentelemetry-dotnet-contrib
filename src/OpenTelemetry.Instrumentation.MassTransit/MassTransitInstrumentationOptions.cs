// <copyright file="MassTransitInstrumentationOptions.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;

namespace OpenTelemetry.Instrumentation.MassTransit;

/// <summary>
/// Options for <see cref="MassTransitInstrumentation"/>.
/// </summary>
public class MassTransitInstrumentationOptions
{
    /// <summary>
    /// Default traced operations.
    /// </summary>
    public static readonly IEnumerable<string> DefaultTracedOperations = new string[]
    {
        OperationName.Transport.Send,
        OperationName.Transport.Receive,
        OperationName.Consumer.Consume,
        OperationName.Consumer.Handle,
    };

    /// <summary>
    /// Gets or sets traced operations set.
    /// </summary>
    public HashSet<string> TracedOperations { get; set; } = new HashSet<string>(DefaultTracedOperations);

    /// <summary>
    /// Gets or sets an action to enrich an Activity.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para>object: the raw object from which additional information can be extracted to enrich the activity.
    /// The type of this object depends on the event.</para>
    /// </remarks>
    public Action<Activity, object> EnrichWithRequestPayload { get; set; }

    /// <summary>
    /// Gets or sets an action to enrich an Activity with <see cref="HttpResponseMessage"/>.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para>object: the raw object from which additional information can be extracted to enrich the activity.</para>
    /// </remarks>
    public Action<Activity, object> EnrichWithResponsePayload { get; set; }

    /// <summary>
    /// Gets or sets an action to enrich an Activity with <see cref="Exception"/>.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para>object: the raw object from which additional information can be extracted to enrich the activity.</para>
    /// </remarks>
    public Action<Activity, object> EnrichWithException { get; set; }
}
