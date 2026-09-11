// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Tests.Mocks;

internal class MockControlledWsTransport : MockControlledTransport
{
    public MockControlledWsTransport(Action? firstSendCallback = null)
        : base(firstSendCallback)
    {
    }

    public override bool RequiresResponseBeforeNextSend => false;
}
