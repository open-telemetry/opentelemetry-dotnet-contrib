// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.Instana.Implementation;
using OpenTelemetry.Exporter.Instana.Implementation.Processors;

namespace OpenTelemetry.Exporter.Instana.Tests;

internal sealed class TestActivityProcessor : IActivityProcessor
{
    public IActivityProcessor? NextProcessor { get; set; }

    public void Process(Activity activity, InstanaSpan instanaSpan)
    {
        // No-op
    }
}
