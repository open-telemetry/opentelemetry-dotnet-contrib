// <copyright file="MyService.cs" company="OpenTelemetry Authors">
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

namespace Examples.Enrichment;

internal sealed class MyService : IMyService
{
    private readonly List<string> statuses = new()
    {
        "Blocked",
        "No blockers",
        "Out of office",
    };

    /// <summary>
    /// Returns daily status.
    /// </summary>
    /// <returns>A tuple with service name and status.</returns>
    public (string Service, string Status) MyDailyStatus()
    {
        var statusNumber = Random.Shared.Next(0, this.statuses.Count);
        return new(nameof(MyService), this.statuses[statusNumber]);
    }
}
