﻿// <copyright file="MassTransitInstrumentationOptions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Contrib.Instrumentation.MassTransit.Implementation;

namespace OpenTelemetry.Contrib.Instrumentation.MassTransit
{
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
    }
}
