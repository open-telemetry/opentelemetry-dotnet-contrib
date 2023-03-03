// <copyright file="LogToActivityEventConversionOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Logs;

/// <summary>
/// Stores options used to convert log messages into <see cref="ActivityEvent"/>s.
/// </summary>
public class LogToActivityEventConversionOptions
{
    /// <summary>
    /// Gets or sets the callback action used to convert log state into <see cref="ActivityEvent"/> tags.
    /// </summary>
    public Action<ActivityTagsCollection, IReadOnlyList<KeyValuePair<string, object?>>> StateConverter { get; set; } = DefaultLogStateConverter.ConvertState;

    /// <summary>
    /// Gets or sets the callback action used to convert log scopes into <see cref="ActivityEvent"/> tags.
    /// </summary>
    public Action<ActivityTagsCollection, int, LogRecordScope> ScopeConverter { get; set; } = DefaultLogStateConverter.ConvertScope;

    /// <summary>
    /// Gets or sets the callback method allowing to filter out particular <see cref="LogRecord"/>.
    /// </summary>
    /// <remarks>
    /// The filter callback receives the <see cref="LogRecord"/> for the
    /// processed logRecord and should return a boolean.
    /// <list type="bullet">
    /// <item>If filter returns <see langword="true"/> the event is collected.</item>
    /// <item>If filter returns <see langword="false"/> or throws an exception the event is filtered out (NOT collected).</item>
    /// </list>
    /// </remarks>
    public Func<LogRecord, bool>? Filter { get; set; }
}
