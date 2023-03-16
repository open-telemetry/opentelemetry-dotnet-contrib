// <copyright file="BaseAspNetCoreEnricher.cs" company="OpenTelemetry Authors">
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
using Microsoft.AspNetCore.Http;

namespace OpenTelemetry.Extensions.Enrichment.AspNetCore;

/// <summary>
/// Represents base ASP.NET Core enricher class.
/// </summary>
/// <typeparam name="T">Typo of enrichment bag to store enrichment tags.</typeparam>
public abstract class BaseAspNetCoreEnricher<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseAspNetCoreEnricher{T}"/> class.
    /// </summary>
    protected BaseAspNetCoreEnricher()
    {
    }

    /// <summary>
    /// Enrich trace with desired tags at HTTP request time.
    /// </summary>
    /// <param name="enrichmentBag">Bag used to store enrichment tags.</param>
    /// <param name="request"><see cref="HttpRequest"/> object associated with the incoming HTTP request for the trace.</param>
    public virtual void EnrichWithHttpRequest(ref T enrichmentBag, HttpRequest request)
    {
    }

    /// <summary>
    /// Enrich trace with desired tags at HTTP response time.
    /// </summary>
    /// <param name="enrichmentBag">Bag used to store enrichment tags.</param>
    /// <param name="response"><see cref="HttpResponse"/> object associated with the incoming HTTP request for the trace.</param>
    public virtual void EnrichWithHttpResponse(ref T enrichmentBag, HttpResponse response)
    {
    }

    /// <summary>
    /// Enrich trace with desired tags at exception time.
    /// </summary>
    /// <param name="enrichmentBag">Bag used to store enrichment tags.</param>
    /// <param name="exception"><see cref="Exception"/> object representing the exception occurred.</param>
    public virtual void EnrichWithException(ref T enrichmentBag, Exception exception)
    {
    }
}
