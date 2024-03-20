// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Extensions.AWS.Trace;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

/// <summary>
/// Uses <see cref="AWSXRayPropagator"/> to inject the current Activity Context and
/// Baggage into the outgoing AWS SDK Request.
/// <para />
/// Must execute after the AWS SDK has marshalled (ie serialized)
/// the outgoing request object so that it can work with the <see cref="IRequest"/>'s
/// <see cref="IRequest.Headers"/>.
/// </summary>
internal class AWSPropagatorPipelineHandler : PipelineHandler
{
    private static readonly AWSXRayPropagator AwsPropagator = new();

    private static readonly Action<IDictionary<string, string>, string, string> Setter = (carrier, name, value) =>
    {
        carrier[name] = value;
    };

    /// <summary>
    /// Rely on the the <see cref="AWSTracingPipelineHandler.Activity"/> for retrieving the current
    /// context.
    /// </summary>
    private readonly AWSTracingPipelineHandler tracingPipelineHandler;

    public AWSPropagatorPipelineHandler(AWSTracingPipelineHandler tracingPipelineHandler)
    {
        this.tracingPipelineHandler = tracingPipelineHandler;
    }

    public override void InvokeSync(IExecutionContext executionContext)
    {
        this.ProcessBeginRequest(executionContext);

        base.InvokeSync(executionContext);
    }

    public override async Task<T> InvokeAsync<T>(IExecutionContext executionContext)
    {
        this.ProcessBeginRequest(executionContext);

        return await base.InvokeAsync<T>(executionContext).ConfigureAwait(false);
    }

    private void ProcessBeginRequest(IExecutionContext executionContext)
    {
        if (this.tracingPipelineHandler.Activity == null)
        {
            return;
        }

        AwsPropagator.Inject(
            new PropagationContext(this.tracingPipelineHandler.Activity.Context, Baggage.Current),
            executionContext.RequestContext.Request.Headers,
            Setter);
    }
}
