// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.SignalR;

namespace TestApp.AspNetCore;

public class TestHub : Hub
{
    public override Task OnConnectedAsync() => base.OnConnectedAsync();

#pragma warning disable IDE0060 // Remove unused parameter
    public void Send(string message)
#pragma warning restore IDE0060 // Remove unused parameter
    {
    }
}
