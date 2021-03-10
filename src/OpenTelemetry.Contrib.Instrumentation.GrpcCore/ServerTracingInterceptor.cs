﻿// <copyright file="ServerTracingInterceptor.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

namespace OpenTelemetry.Contrib.Instrumentation.GrpcCore
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using global::Grpc.Core;
    using global::Grpc.Core.Interceptors;
    using OpenTelemetry.Context.Propagation;

    /// <summary>
    /// A service interceptor that starts and stops an Activity for each inbound RPC.
    /// </summary>
    /// <seealso cref="global::Grpc.Core.Interceptors.Interceptor" />
    public class ServerTracingInterceptor : Interceptor
    {
        /// <summary>
        /// The options.
        /// </summary>
        private readonly ServerTracingInterceptorOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerTracingInterceptor"/> class.
        /// </summary>
        /// <param name="options">The options.</param>
        public ServerTracingInterceptor(ServerTracingInterceptorOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc/>
        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            using var rpcScope = new ServerRpcScope<TRequest, TResponse>(context, this.options);

            try
            {
                rpcScope.RecordRequest(request);
                var response = await continuation(request, context).ConfigureAwait(false);
                rpcScope.RecordResponse(response);
                rpcScope.Complete();
                return response;
            }
            catch (Exception e)
            {
                rpcScope.CompleteWithException(e);
                throw;
            }
        }

        /// <inheritdoc/>
        public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
            IAsyncStreamReader<TRequest> requestStream,
            ServerCallContext context,
            ClientStreamingServerMethod<TRequest, TResponse> continuation)
        {
            using var rpcScope = new ServerRpcScope<TRequest, TResponse>(context, this.options);

            try
            {
                var requestStreamReaderProxy = new AsyncStreamReaderProxy<TRequest>(
                    requestStream,
                    rpcScope.RecordRequest);

                var response = await continuation(requestStreamReaderProxy, context).ConfigureAwait(false);
                rpcScope.RecordResponse(response);
                rpcScope.Complete();
                return response;
            }
            catch (Exception e)
            {
                rpcScope.CompleteWithException(e);
                throw;
            }
        }

        /// <inheritdoc/>
        public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
            TRequest request,
            IServerStreamWriter<TResponse> responseStream,
            ServerCallContext context,
            ServerStreamingServerMethod<TRequest, TResponse> continuation)
        {
            using var rpcScope = new ServerRpcScope<TRequest, TResponse>(context, this.options);

            try
            {
                rpcScope.RecordRequest(request);

                var responseStreamProxy = new ServerStreamWriterProxy<TResponse>(
                    responseStream,
                    rpcScope.RecordResponse);

                await continuation(request, responseStreamProxy, context).ConfigureAwait(false);
                rpcScope.Complete();
            }
            catch (Exception e)
            {
                rpcScope.CompleteWithException(e);
                throw;
            }
        }

        /// <inheritdoc/>
        public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation)
        {
            using var rpcScope = new ServerRpcScope<TRequest, TResponse>(context, this.options);

            try
            {
                var requestStreamReaderProxy = new AsyncStreamReaderProxy<TRequest>(
                    requestStream,
                    rpcScope.RecordRequest);

                var responseStreamProxy = new ServerStreamWriterProxy<TResponse>(
                    responseStream,
                    rpcScope.RecordResponse);

                await continuation(requestStreamReaderProxy, responseStreamProxy, context).ConfigureAwait(false);
                rpcScope.Complete();
            }
            catch (Exception e)
            {
                rpcScope.CompleteWithException(e);
                throw;
            }
        }

        /// <summary>
        /// A class to help track the lifetime of a service-side RPC.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        private class ServerRpcScope<TRequest, TResponse> : RpcScope<TRequest, TResponse>
            where TRequest : class
            where TResponse : class
        {
            /// <summary>
            /// The metadata setter action.
            /// </summary>
            private static readonly Func<Metadata, string, IEnumerable<string>> MetadataGetter = (metadata, key) =>
            {
                for (var i = 0; i < metadata.Count; i++)
                {
                    var entry = metadata[i];
                    if (entry.Key.Equals(key))
                    {
                        return new string[1] { entry.Value };
                    }
                }

                return Enumerable.Empty<string>();
            };

            /// <summary>
            /// Initializes a new instance of the <see cref="ServerRpcScope{TRequest, TResponse}"/> class.
            /// </summary>
            /// <param name="context">The context.</param>
            /// <param name="options">The options.</param>
            public ServerRpcScope(ServerCallContext context, ServerTracingInterceptorOptions options)
                : base(context.Method, options.RecordMessageEvents)
            {
                if (!GrpcCoreInstrumentation.ActivitySource.HasListeners())
                {
                    return;
                }

                var currentContext = Activity.Current?.Context;

                // Extract the SpanContext, if any from the headers
                var metadata = context.RequestHeaders;
                if (metadata != null)
                {
                    var propagationContext = options.Propagator.Extract(new PropagationContext(currentContext ?? default, Baggage.Current), metadata, MetadataGetter);
                    if (propagationContext.ActivityContext.IsValid())
                    {
                        currentContext = propagationContext.ActivityContext;
                    }

                    if (propagationContext.Baggage != default)
                    {
                        Baggage.Current = propagationContext.Baggage;
                    }
                }

                // This if block is for unit testing only.
                IEnumerable<KeyValuePair<string, object>> customTags = null;
                if (options.ActivityIdentifierValue != default)
                {
                    customTags = new List<KeyValuePair<string, object>>
                    {
                        new KeyValuePair<string, object>(SemanticConventions.AttributeActivityIdentifier, options.ActivityIdentifierValue),
                    };
                }

                var activity = GrpcCoreInstrumentation.ActivitySource.StartActivity(
                    this.FullServiceName,
                    ActivityKind.Server,
                    currentContext ?? default,
                    tags: customTags);

                this.SetActivity(activity);
            }
        }
    }
}
