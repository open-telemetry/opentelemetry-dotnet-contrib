// <copyright file="PooledBagPolicy.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.ObjectPool;

namespace OpenTelemetry.Extensions.Enrichment;

public sealed class PooledBagPolicy<T> : PooledObjectPolicy<T>
    where T : BaseEnrichmentBag, new()
{
    public PooledBagPolicy()
    {
    }

    public static PooledBagPolicy<T> Instance { get; } = new();

    public override T Create() => new();

    public override bool Return(T obj)
    {
        obj?.Clear();
        return true;
    }
}
