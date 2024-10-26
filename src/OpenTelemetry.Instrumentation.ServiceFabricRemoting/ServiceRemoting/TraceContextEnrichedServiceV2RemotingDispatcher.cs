// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0using System.Fabric;

using System.Diagnostics;
using System.Fabric;
using System.Text;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Runtime;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

/// <summary>
/// Provides an implementation of Microsoft.ServiceFabric.Services.Remoting.V2.Runtime.IServiceRemotingMessageHandler
/// that can dispatch messages to the service implementing Microsoft.ServiceFabric.Services.Remoting.IService interface.
/// </summary>
public class TraceContextEnrichedServiceV2RemotingDispatcher : ServiceRemotingMessageDispatcher
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TraceContextEnrichedServiceV2RemotingDispatcher"/> class.
    /// </summary>
    /// <param name="serviceContext">Service context.</param>
    /// <param name="serviceImplementation">Service implementation that implements interfaces of type Microsoft.ServiceFabric.Services.Remoting.IService.</param>
    public TraceContextEnrichedServiceV2RemotingDispatcher(ServiceContext serviceContext, IService serviceImplementation)
        : base(serviceContext, serviceImplementation)
    {
    }

    /// <summary>
    /// Handles a message from the client that requires a response from the service.
    /// </summary>
    /// <param name="requestContext">Request context - contains additional information about the request.</param>
    /// <param name="requestMessage">Request message.</param>
    /// <returns> The response for the received request.</returns>
    public override async Task<IServiceRemotingResponseMessage> HandleRequestResponseAsync(IServiceRemotingRequestContext requestContext, IServiceRemotingRequestMessage requestMessage)
    {
        Guard.ThrowIfNull(requestMessage);

        if (ServiceFabricRemotingActivitySource.Options?.Filter?.Invoke(requestMessage) == false)
        {
            // If we filter out the request we don't need to process anything related to the activity
            return await base.HandleRequestResponseAsync(requestContext, requestMessage).ConfigureAwait(false);
        }
        else
        {
            IServiceRemotingRequestMessageHeader requestMessageHeader = requestMessage.GetHeader();
            Guard.ThrowIfNull(requestMessageHeader, "requestMessage.GetHeader()");

            // Extract the PropagationContext of the upstream parent from the message headers.
            PropagationContext parentContext = Propagator.Extract(default, requestMessageHeader, this.ExtractTraceContextFromRequestMessageHeader);
            Baggage.Current = parentContext.Baggage;

            string activityName = requestMessageHeader?.MethodName ?? ServiceFabricRemotingActivitySource.IncomingRequestActivityName;

            using (Activity? activity = ServiceFabricRemotingActivitySource.ActivitySource.StartActivity(activityName, ActivityKind.Server, parentContext.ActivityContext))
            {
                IServiceRemotingResponseMessage responseMessage = await base.HandleRequestResponseAsync(requestContext, requestMessage).ConfigureAwait(false);

                return responseMessage;
            }
        }
    }

    private IEnumerable<string> ExtractTraceContextFromRequestMessageHeader(IServiceRemotingRequestMessageHeader requestMessageHeader, string headerKey)
    {
        if (requestMessageHeader.TryGetHeaderValue(headerKey, out byte[] headerValueAsBytes))
        {
            string headerValue = Encoding.UTF8.GetString(headerValueAsBytes);

            return new[] { headerValue };
        }

        return Enumerable.Empty<string>();
    }
}
