// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Remoting.Implementation;
using RemotingContext = System.Runtime.Remoting.Contexts.Context;

namespace OpenTelemetry.Instrumentation.Remoting;

internal sealed class RemotingInstrumentation : IDisposable
{
    internal static int RegCount;
    private static readonly object LockObj = new();

    public RemotingInstrumentation(RemotingInstrumentationOptions options)
    {
        // Just in case we are called multiple times, make sure we register
        // the dynamic sink only once per AppDomain
        lock (LockObj)
        {
            if (RegCount == 0)
            {
                // See https://docs.microsoft.com/dotnet/api/system.runtime.remoting.contexts.context.registerdynamicproperty
                // This will register our dynamic sink to listen to all calls leaving or entering
                // current AppDomain.
                RemotingContext.RegisterDynamicProperty(
                    new TelemetryDynamicSinkProvider(options),
                    null,
                    RemotingContext.DefaultContext);
            }

            RegCount++;
        }
    }

    public void Dispose()
    {
        // If there were multiple registration attempts, assume that each
        // of those registrations will also try to un-register.
        lock (LockObj)
        {
            RegCount--;
            if (RegCount == 0)
            {
                // When the last registration disposes, remove the property.
                RemotingContext.UnregisterDynamicProperty(
                    TelemetryDynamicSinkProvider.DynamicPropertyName,
                    null,
                    RemotingContext.DefaultContext);
            }
        }
    }
}
