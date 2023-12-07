// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Examples.Wcf.Server;

[ServiceBehavior(
    Namespace = "http://opentelemetry.io/",
    ConcurrencyMode = ConcurrencyMode.Multiple,
    InstanceContextMode = InstanceContextMode.Single,
    UseSynchronizationContext = false,
    Name = "StatusService")]
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
