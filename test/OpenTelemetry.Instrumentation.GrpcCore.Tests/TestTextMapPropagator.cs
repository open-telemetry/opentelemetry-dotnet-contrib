// <copyright file="TestTextMapPropagator.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Collections.Generic;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Instrumentation.GrpcCore.Tests;

internal class TestTextMapPropagator : TextMapPropagator
{
    public Action<PropagationContext, object, Action<object, string, string>> OnInject { get; set; }

    public override ISet<string> Fields => throw new NotImplementedException();

    public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
    {
        throw new NotImplementedException();
    }

    public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
    {
        var newAction = new Action<T, string, string>((c, k, v) => setter(c, k, v));
        this.OnInject?.Invoke(
            context,
            carrier,
            new Action<object, string, string>((c, k, v) => setter((T)c, k, v)));
    }
}
