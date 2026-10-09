// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Wcf;

internal sealed class ActionMetadata
{
    public ActionMetadata(string? contractName, string operationName, bool isOneWay = false)
    {
        this.ContractName = contractName;
        this.OperationName = operationName;
        this.IsOneWay = isOneWay;
    }

    public string? ContractName { get; set; }

    public string OperationName { get; set; }

    public bool IsOneWay { get; set; }
}
