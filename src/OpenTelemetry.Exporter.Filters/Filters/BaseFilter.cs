// <copyright file="BaseFilter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Filters;

/// <summary>
/// Base class for Filters, defines the interfaces.
/// </summary>
/// <typeparam name="T">A generic type parameter for Filter.</typeparam>
public abstract class BaseFilter<T> : IDisposable
{
    /// <summary>
    /// interface to decide whether to filter the data .
    /// </summary>
    /// <param name="t">generic type parameter.</param>
    /// <returns>if true returned, data will be dropped. Else will be kept.</returns>
    public abstract bool ShouldFilter(T t);

    /// <summary>
    /// desciption of the filter.
    /// </summary>
    /// <returns>desciption of filter.</returns>
    public abstract string GetDescription();

    /// <summary>
    /// name of the filter.
    /// </summary>
    /// <returns>name of filter.</returns>
    public virtual string GetName()
    {
        return this.GetType().Name;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by this class and optionally
    /// releases the managed resources.
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/> to release both managed and unmanaged resources;
    /// <see langword="false"/> to release only unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
