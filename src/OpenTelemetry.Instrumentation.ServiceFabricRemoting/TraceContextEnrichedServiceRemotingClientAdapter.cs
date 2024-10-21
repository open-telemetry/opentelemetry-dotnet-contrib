// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0using System.Fabric;

using System.Diagnostics;
using System.Fabric;
using System.Text;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

public class TraceContextEnrichedServiceRemotingClientAdapter : IServiceRemotingClient
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    private readonly IServiceRemotingClient innerClient;

    public TraceContextEnrichedServiceRemotingClientAdapter(IServiceRemotingClient remotingClient)
    {
        this.innerClient = remotingClient;
    }

    public IServiceRemotingClient InnerClient
    {
        get { return this.innerClient; }
    }

    public ResolvedServicePartition ResolvedServicePartition
    {
        get { return this.InnerClient.ResolvedServicePartition; }
        set { this.InnerClient.ResolvedServicePartition = value; }
    }

    public string ListenerName
    {
        get { return this.InnerClient.ListenerName; }
        set { this.InnerClient.ListenerName = value; }
    }

    public ResolvedServiceEndpoint Endpoint
    {
        get { return this.InnerClient.Endpoint; }
        set { this.InnerClient.Endpoint = value; }
    }

    public async Task<IServiceRemotingResponseMessage> RequestResponseAsync(IServiceRemotingRequestMessage requestMessage)
    {
        if (ServiceFabricRemotingActivitySource.Options?.Filter?.Invoke(requestMessage) == false)
        {
            //If we filter out the request we don't need to process anything related to the activity
            return await this.innerClient.RequestResponseAsync(requestMessage).ConfigureAwait(false);
        }
        else
        {
            IServiceRemotingRequestMessageHeader requestMessageHeader = requestMessage?.GetHeader();
            string activityName = requestMessageHeader?.MethodName ?? ServiceFabricRemotingActivitySource.OutgoingRequestActivityName;

            using (Activity activity = ServiceFabricRemotingActivitySource.ActivitySource.StartActivity(activityName, ActivityKind.Client))
            {
                // Depending on Sampling (and whether a listener is registered or not), the activity above may not be created.
                // If it is created, then propagate its context.
                if (activity != null)
                {
                    try
                    {
                        ServiceFabricRemotingActivitySource.Options?.EnrichAtClientFromRequest?.Invoke(activity, requestMessage);
                    }
                    catch (Exception)
                    {
                        //TODO: Log error
                    }

                    try
                    {
                        // Inject the ActivityContext into the message headers to propagate trace context and Baggage to the receiving service.
                        Propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), requestMessageHeader, this.InjectTraceContextIntoServiceRemotingRequestMessageHeader);
                    }
                    catch (Exception ex)
                    {
                        activity.SetStatus(ActivityStatusCode.Error, $"Error trying to inject the context in the remoting request: '{ex.Message}'");
                    }
                }

                try
                {
                    IServiceRemotingResponseMessage responseMessage = await this.innerClient.RequestResponseAsync(requestMessage).ConfigureAwait(false);

                    if (activity != null)
                    {
                        try
                        {
                            ServiceFabricRemotingActivitySource.Options?.EnrichAtClientFromResponse?.Invoke(activity, responseMessage, /* exception */ null);
                        }
                        catch (Exception)
                        {
                            //TODO: Log error
                        }
                    }

                    return responseMessage;
                }
                catch (Exception ex)
                {
                    if (activity != null)
                    {
                        activity.SetStatus(Status.Error);

                        if (ServiceFabricRemotingActivitySource.Options?.RecordException == true)
                        {
                            activity.RecordException(ex);
                        }

                        try
                        {
                            ServiceFabricRemotingActivitySource.Options?.EnrichAtClientFromResponse?.Invoke(activity, null, ex);
                        }
                        catch (Exception)
                        {
                            //TODO: Log error
                        }
                    }
                    throw;
                }
            }
        }
    }

    public void SendOneWay(IServiceRemotingRequestMessage requestMessage)
    {
        this.InnerClient.SendOneWay(requestMessage);
    }

    private void InjectTraceContextIntoServiceRemotingRequestMessageHeader(IServiceRemotingRequestMessageHeader requestMessageHeader, string key, string value)
    {
        if (!requestMessageHeader.TryGetHeaderValue(key, out byte[] _))
        {
            byte[] valueAsBytes = Encoding.UTF8.GetBytes(value);

            requestMessageHeader.AddHeader(key, valueAsBytes);
        }
    }
}
