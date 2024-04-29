// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

#pragma warning disable IDE0005 // Using directive is unnecessary.
using System.Collections.Generic;
#pragma warning restore IDE0005 // Using directive is unnecessary.
using System.Diagnostics;

namespace OpenTelemetry.Tests;

internal class TestActivityExportProcessor : SimpleActivityExportProcessor
{
    public List<Activity> ExportedItems = new List<Activity>();

    public TestActivityExportProcessor(BaseExporter<Activity> exporter)
        : base(exporter)
    {
    }

    protected override void OnExport(Activity data)
    {
        this.ExportedItems.Add(data);
    }
}
