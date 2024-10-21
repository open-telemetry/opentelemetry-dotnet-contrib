using System.Diagnostics;
// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0using System.Fabric;

using System.Text;
using Microsoft.ServiceFabric.Actors.Remoting.V2.Runtime;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Runtime;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

/// <summary>
/// Provides an implementation of Microsoft.ServiceFabric.Services.Remoting.V2.Runtime.IServiceRemotingMessageHandler
/// that can dispatch messages to an actor service and to the actors hosted in the service
/// </summary>
public class TraceContextEnrichedActorServiceV2RemotingDispatcher : ActorServiceRemotingDispatcher
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    public TraceContextEnrichedActorServiceV2RemotingDispatcher(ActorService service)
        : base(service, serviceRemotingRequestMessageBodyFactory: null)
    {
    }

    public override async Task<IServiceRemotingResponseMessage> HandleRequestResponseAsync(IServiceRemotingRequestContext requestContext, IServiceRemotingRequestMessage requestMessage)
    {
        if (ServiceFabricRemotingActivitySource.Options?.Filter?.Invoke(requestMessage) == false)
        {
            //If we filter out the request we don't need to process anything related to the activity
            return await base.HandleRequestResponseAsync(requestContext, requestMessage).ConfigureAwait(false);
        }
        else
        {
            IServiceRemotingRequestMessageHeader requestMessageHeader = requestMessage?.GetHeader();

            // Extract the PropagationContext of the upstream parent from the message headers.
            PropagationContext parentContext = Propagator.Extract(default, requestMessageHeader, this.ExtractTraceContextFromRequestMessageHeader);
            Baggage.Current = parentContext.Baggage;

            string activityName = requestMessageHeader?.MethodName ?? ServiceFabricRemotingActivitySource.IncomingRequestActivityName;

            using (Activity activity = ServiceFabricRemotingActivitySource.ActivitySource.StartActivity(activityName, ActivityKind.Server, parentContext.ActivityContext))
            {
                IServiceRemotingResponseMessage responseMessage = await base.HandleRequestResponseAsync(requestContext, requestMessage);

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
