// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Tests.Mocks;

internal sealed class MockControlledHttpTransport : MockControlledTransport
{
    public MockControlledHttpTransport(Action? firstSendCallback = null)
        : base(firstSendCallback)
    {
    }

    public override bool RequiresResponseBeforeNextSend => true;
}
