// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Microsoft.Extensions.Logging;

internal sealed class AddTraceIdLoggerProvider : ILoggerProvider
{
    private readonly ILoggerProvider innerLoggerProvider;

    public AddTraceIdLoggerProvider(ILoggerProvider baseProvider)
    {
        this.innerLoggerProvider = baseProvider;
    }

    public ILogger CreateLogger(string categoryName) => new AddTraceIdLogger(this.innerLoggerProvider.CreateLogger(categoryName));

    public void Dispose() => this.innerLoggerProvider.Dispose();
}
