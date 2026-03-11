// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.ServiceModel;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

internal static class WcfTestExtensions
{
    internal static void AbortOrClose(this ServiceClient client)
    {
        if (client.State == CommunicationState.Faulted)
        {
            client.Abort();
        }
        else
        {
            client.Close();
        }
    }
}
