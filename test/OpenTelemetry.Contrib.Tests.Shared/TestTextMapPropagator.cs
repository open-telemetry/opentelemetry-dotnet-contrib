// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Tests;

internal class TestTextMapPropagator : TextMapPropagator
{
    public Action? Extracted { get; set; }

    public override ISet<string> Fields => throw new NotImplementedException();

    public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
    {
        this.Extracted?.Invoke();
        return context;
    }

    public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
    {
        throw new NotImplementedException();
    }
}
