// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0using System.Fabric;

using System.Diagnostics;
using System.Fabric;
using System.Text;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Runtime;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

public class TraceContextEnrichedServiceV2RemotingDispatcher : ServiceRemotingMessageDispatcher
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    public TraceContextEnrichedServiceV2RemotingDispatcher(ServiceContext serviceContext, IService serviceImplementation)
        : base(serviceContext, serviceImplementation)
    {
    }

    // Summary:
    //     Handles a message from the client that requires a response from the service.
    //
    //
    // Parameters:
    //   requestContext:
    //     Request context - contains additional information about the request
    //
    //   requestMessage:
    //     Request message
    //
    // Returns:
    //     A System.Threading.Tasks.Task representing the asynchronous operation. The result
    //     of the task is the response for the received request.
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
