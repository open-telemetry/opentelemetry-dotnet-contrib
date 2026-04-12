// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

public class MyTestActor : Actor, IMyTestActor
{
    public MyTestActor(ActorService actorService, ActorId actorId)
        : base(actorService, actorId)
    {
    }

    public Task<ServiceResponse> TestContextPropagation(string valueToReturn)
    {
        var activityContext = Activity.Current!.Context;
        var baggage = Baggage.Current;

        var serviceResponse = new ServiceResponse
        {
            ParameterValue = valueToReturn,
            ActivityContext = activityContext,
            Baggage = baggage,
        };

        return Task.FromResult(serviceResponse);
    }
}
