// <copyright file="ServiceConnectInstrumentationOptions.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using ServiceConnect.Interfaces;

namespace OpenTelemetry.Instrumentation.ServiceConnect;

public class ServiceConnectInstrumentationOptions
{
    /// <summary>
    /// Gets or sets an action to enrich an Activity from message.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para><see cref="Message"/>: the message being published/consumed.</para>
    /// </remarks>
    public Action<Activity, Message>? EnrichWithMessage { get; set; }

    /// <summary>
    /// Gets or sets an action to enrich an Activity from message.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para><see cref="byte"/>[]: the data of the message being published/consumed.</para>
    /// </remarks>
    public Action<Activity, byte[]>? EnrichWithMessageBytes { get; set; }
}
