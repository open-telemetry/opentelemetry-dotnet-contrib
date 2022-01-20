// <copyright file="MySqlDataInstrumentationOptions.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;

using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Instrumentation.EventCounterListener
{

    /// <summary>
    /// The Event Counter to listen to
    /// </summary>
    public class EventCounter
    {
        public string Name { get; set; }

        public string Type { get; set; }
    }
}
