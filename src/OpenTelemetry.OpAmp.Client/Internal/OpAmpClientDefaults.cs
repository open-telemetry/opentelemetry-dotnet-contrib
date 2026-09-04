// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Internal;

internal static class OpAmpClientDefaults
{
    internal const int MaxPendingCustomMessages = 2048;
    internal const int MaxPendingCustomMessageBytes = 64 * 1024 * 1024;
}
