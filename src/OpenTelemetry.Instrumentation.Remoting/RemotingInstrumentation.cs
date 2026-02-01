// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Remoting.Implementation;
using RemotingContext = System.Runtime.Remoting.Contexts.Context;

namespace OpenTelemetry.Instrumentation.Remoting;

internal class RemotingInstrumentation : IDisposable
{
    private static readonly object LockObj = new();
    private static int regCount;

    public RemotingInstrumentation(RemotingInstrumentationOptions options)
    {
        // Just in case we are called multiple times, make sure we register
        // the dynamic sink only once per AppDomain
        lock (LockObj)
        {
            if (regCount == 0)
            {
                // See https://docs.microsoft.com/dotnet/api/system.runtime.remoting.contexts.context.registerdynamicproperty
                // This will register our dynamic sink to listen to all calls leaving or entering
                // current AppDomain.
                RemotingContext.RegisterDynamicProperty(
                    new TelemetryDynamicSinkProvider(options),
                    null,
                    RemotingContext.DefaultContext);
            }

            regCount++;
        }
    }

    public void Dispose()
    {
        // If there were multiple registration attempts, assume that each
        // of those registrations will also try to un-register.
        lock (LockObj)
        {
            regCount--;
            if (regCount == 0)
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
