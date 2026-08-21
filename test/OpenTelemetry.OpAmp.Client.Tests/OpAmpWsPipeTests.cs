// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Tests.Mocks;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class OpAmpWsPipeTests : OpAmpPipeTests
{
    internal override MockControlledTransport GetTransport(Action? firstSendCallback = null) => new MockControlledWsTransport(firstSendCallback);
}
