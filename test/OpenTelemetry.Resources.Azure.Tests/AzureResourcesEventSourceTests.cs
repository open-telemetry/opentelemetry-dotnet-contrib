// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Resources.Azure.Tests;

public class AzureResourcesEventSourceTests
{
    [Fact]
    public void AzureResourcesEventSourceHasUniqueEventIds()
        => EventSourceTestHelper.ValidateEventSourceIds<AzureResourcesEventSource>();

    [Fact]
    public void AzureResourcesEventSourceLogsDetectionFailures()
    {
        using var listener = new InMemoryEventListener(AzureResourcesEventSource.Log, EventLevel.Warning);
        var failureMessage = $"detection failed {Guid.NewGuid():N}";
        var exception = new InvalidOperationException(failureMessage);
        var logActions = new (Action<Exception> LogFailure, string EventName)[]
        {
            (AzureResourcesEventSource.Log.FailedToDetectAppServiceResources, nameof(AzureResourcesEventSource.FailedToDetectAppServiceResources)),
            (AzureResourcesEventSource.Log.FailedToDetectAzureContainerAppResources, nameof(AzureResourcesEventSource.FailedToDetectAzureContainerAppResources)),
            (AzureResourcesEventSource.Log.FailedToDetectAzureVMResources, nameof(AzureResourcesEventSource.FailedToDetectAzureVMResources)),
            (AzureResourcesEventSource.Log.FailedToDetectAzureFunctionsResources, nameof(AzureResourcesEventSource.FailedToDetectAzureFunctionsResources)),
        };

        foreach (var logAction in logActions)
        {
            logAction.LogFailure(exception);

            Assert.Contains(
                listener.Events,
                e => e.EventName == logAction.EventName
                    && e.Payload![0] is string exceptionMessage
                    && exceptionMessage.Contains(failureMessage));
        }
    }
}
