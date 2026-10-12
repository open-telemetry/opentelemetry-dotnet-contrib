// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.ServiceModel;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

[ServiceContract(CallbackContract = typeof(IOneWayDuplexCallback))]
internal interface IOneWayDuplexServiceContract
{
    [OperationContract(IsOneWay = true)]
    void ExecuteWithOneWay(ServiceRequest request);
}

internal interface IOneWayDuplexCallback
{
    [OperationContract(IsOneWay = true)]
    void OnCallback();
}
