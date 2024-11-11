﻿// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.ServiceFabric.Services.Remoting;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

public interface IMyStatefulService : IService
{
    Task<ServiceResponse> TestContextPropagation(string valueToReturn);
}
