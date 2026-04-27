// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Instana.Implementation.Processors;

internal interface IActivityProcessor
{
    IActivityProcessor? NextProcessor { get; set; }

    void Process(System.Diagnostics.Activity activity, InstanaSpan instanaSpan);
}
