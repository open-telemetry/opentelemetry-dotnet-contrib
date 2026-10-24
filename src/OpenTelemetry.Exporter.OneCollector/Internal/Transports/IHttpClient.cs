// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif

namespace OpenTelemetry.Exporter.OneCollector;

internal interface IHttpClient
{
    HttpResponseMessage Send(
        HttpRequestMessage request,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken);
}
