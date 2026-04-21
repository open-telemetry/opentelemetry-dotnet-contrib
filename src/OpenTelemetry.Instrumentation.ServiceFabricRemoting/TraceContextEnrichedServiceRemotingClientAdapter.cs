// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Fabric;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

/// <summary>
/// An IServiceRemotingClient that enriches the outgoing request with the current Activity (if any).
/// </summary>
internal class TraceContextEnrichedServiceRemotingClientAdapter : IServiceRemotingClient
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    public TraceContextEnrichedServiceRemotingClientAdapter(IServiceRemotingClient remotingClient)
    {
        this.InnerClient = remotingClient;
    }

    public IServiceRemotingClient InnerClient { get; }

    public ResolvedServicePartition ResolvedServicePartition
    {
        get => this.InnerClient.ResolvedServicePartition;
        set => this.InnerClient.ResolvedServicePartition = value;
    }

    public string ListenerName
    {
        get => this.InnerClient.ListenerName;
        set => this.InnerClient.ListenerName = value;
    }

    public ResolvedServiceEndpoint Endpoint
    {
        get => this.InnerClient.Endpoint;
        set => this.InnerClient.Endpoint = value;
    }

    public async Task<IServiceRemotingResponseMessage> RequestResponseAsync(IServiceRemotingRequestMessage requestMessage)
    {
        Guard.ThrowIfNull(requestMessage);

        IServiceRemotingRequestMessageHeader requestMessageHeader = requestMessage.GetHeader();
        Guard.ThrowIfNull(requestMessageHeader, "requestMessage.GetHeader()");

        string? serverAddress = ServiceFabricRemotingUtils.GetServerAddress(this.InnerClient);
        long startTimestamp = Stopwatch.GetTimestamp();
        string? errorType = null;

        // Filter only gates tracing. Metrics are always emitted so rate / error-rate dashboards
        // reflect all real traffic, not just the requests selected for tracing.
        bool shouldTrace = ServiceFabricRemotingActivitySource.Options?.Filter?.Invoke(requestMessage) != false;

        string activityName = requestMessageHeader.MethodName ?? ServiceFabricRemotingActivitySource.OutgoingRequestActivityName;

        using Activity? activity = shouldTrace
            ? ServiceFabricRemotingActivitySource.ActivitySource.StartActivity(activityName, ActivityKind.Client)
            : null;

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
                // TODO: Log error
            }

            try
            {
                // Inject the ActivityContext into the message headers to propagate trace context and Baggage to the receiving service.
                Propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), requestMessageHeader, ServiceFabricRemotingUtils.InjectTraceContextIntoServiceRemotingRequestMessageHeader);
            }
            catch (Exception ex)
            {
                activity.SetStatus(ActivityStatusCode.Error, $"Error trying to inject the context in the remoting request: '{ex.Message}'");
            }
        }

        try
        {
            IServiceRemotingResponseMessage responseMessage = await this.InnerClient.RequestResponseAsync(requestMessage).ConfigureAwait(false);

            if (activity != null)
            {
                try
                {
                    ServiceFabricRemotingActivitySource.Options?.EnrichAtClientFromResponse?.Invoke(activity, responseMessage, /* exception */ null);
                }
                catch (Exception)
                {
                    // TODO: Log error
                }
            }

            return responseMessage;
        }
        catch (Exception ex)
        {
            errorType = ex.GetType().FullName;

            if (activity != null)
            {
                activity.SetStatus(ActivityStatusCode.Error);

                if (ServiceFabricRemotingActivitySource.Options?.AddExceptionAtClient == true)
                {
                    activity.AddException(ex);
                }

                try
                {
                    ServiceFabricRemotingActivitySource.Options?.EnrichAtClientFromResponse?.Invoke(activity, /* serviceRemotingResponseMessage */ null, ex);
                }
                catch (Exception)
                {
                    // TODO: Log error
                }
            }

            throw;
        }
        finally
        {
            if (ServiceFabricRemotingMetrics.ClientCallDuration.Enabled)
            {
                TagList tags = default;
                tags.Add(SemanticConventions.AttributeRpcSystemName, ServiceFabricRemotingSemanticConventions.RpcSystemServiceFabricRemoting);
                if (requestMessageHeader.MethodName != null)
                {
                    tags.Add(SemanticConventions.AttributeRpcMethod, requestMessageHeader.MethodName);
                }

                if (errorType != null)
                {
                    tags.Add(SemanticConventions.AttributeErrorType, errorType);
                }

                if (serverAddress != null)
                {
                    tags.Add(SemanticConventions.AttributeServerAddress, serverAddress);
                }

                double elapsedSeconds = (Stopwatch.GetTimestamp() - startTimestamp) / (double)Stopwatch.Frequency;
                ServiceFabricRemotingMetrics.ClientCallDuration.Record(elapsedSeconds, tags);
            }
        }
    }

    public void SendOneWay(IServiceRemotingRequestMessage requestMessage)
        => this.InnerClient.SendOneWay(requestMessage);
}
