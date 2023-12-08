// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;

namespace Examples.Wcf.Server.AspNetFramework;

public class StatusService : IStatusServiceContract
{
    public Task<StatusResponse> PingAsync(StatusRequest request)
    {
        return Task.FromResult(
            new StatusResponse
            {
                ServerTime = DateTimeOffset.UtcNow,
            });
    }
}
