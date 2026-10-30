// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace OpenTelemetry.Instrumentation.SshNet.Tests;

internal static class InterfaceFake
{
    public static InterfaceFake<T> Create<T>()
        where T : class
    {
        var instance = DispatchProxy.Create<T, InterfaceFake<T>>();
        var fake = (InterfaceFake<T>)(object)instance;
        fake.Instance = instance;
        return fake;
    }
}
