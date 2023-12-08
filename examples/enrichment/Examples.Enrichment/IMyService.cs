// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Examples.Enrichment;

public interface IMyService
{
    public (string Service, string Status) MyDailyStatus();
}
