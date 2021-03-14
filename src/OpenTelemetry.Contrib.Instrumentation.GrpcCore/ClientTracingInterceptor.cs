﻿// <copyright file="ClientTracingInterceptor.cs" company="OpenTelemetry Authors">
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
    using global::Grpc.Core;
    using global::Grpc.Core.Interceptors;
    using OpenTelemetry.Context.Propagation;

    /// <summary>
    /// A client interceptor that starts and stops an Activity for each outbound RPC.
    /// </summary>
    /// <seealso cref="global::Grpc.Core.Interceptors.Interceptor" />
    public class ClientTracingInterceptor : Interceptor
    {
        /// <summary>
        /// The options.
        /// </summary>
        private readonly ClientTracingInterceptorOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientTracingInterceptor"/> class.
        /// </summary>
        /// <param name="options">The options.</param>
        public ClientTracingInterceptor(ClientTracingInterceptorOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc/>
        public override TResponse BlockingUnaryCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            ClientRpcScope<TRequest, TResponse> rpcScope = null;

            try
            {
                rpcScope = new ClientRpcScope<TRequest, TResponse>(context, this.options);
                rpcScope.RecordRequest(request);
                var response = continuation(request, rpcScope.Context);
                rpcScope.RecordResponse(response);
                rpcScope.Complete();
                return response;
            }
            catch (Exception e)
            {
                rpcScope?.CompleteWithException(e);
                throw;
            }
            finally
            {
                rpcScope?.RestoreParentActivity();
                rpcScope?.Dispose();
            }
        }

        /// <inheritdoc/>
        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            ClientRpcScope<TRequest, TResponse> rpcScope = null;

            try
            {
                rpcScope = new ClientRpcScope<TRequest, TResponse>(context, this.options);
                rpcScope.RecordRequest(request);
                var responseContinuation = continuation(request, rpcScope.Context);
                var responseAsync = responseContinuation.ResponseAsync.ContinueWith(
                    responseTask =>
                    {
                        try
                        {
                            var response = responseTask.Result;
                            rpcScope.RecordResponse(response);
                            rpcScope.Complete();
                            return response;
                        }
                        catch (AggregateException ex)
                        {
                            rpcScope.CompleteWithException(ex.InnerException);
                            throw ex.InnerException;
                        }
                    });

                return new AsyncUnaryCall<TResponse>(
                    responseAsync,
                    responseContinuation.ResponseHeadersAsync,
                    responseContinuation.GetStatus,
                    responseContinuation.GetTrailers,
                    responseContinuation.WithBestEffortDispose(rpcScope));
            }
            catch (Exception e)
            {
                rpcScope?.CompleteWithException(e);
                throw;
            }
            finally
            {
                rpcScope?.RestoreParentActivity();
            }
        }

        /// <inheritdoc/>
        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            ClientRpcScope<TRequest, TResponse> rpcScope = null;

            try
            {
                rpcScope = new ClientRpcScope<TRequest, TResponse>(context, this.options);
                var responseContinuation = continuation(rpcScope.Context);
                var clientRequestStreamProxy = new ClientStreamWriterProxy<TRequest>(responseContinuation.RequestStream, rpcScope.RecordRequest);
                var responseAsync = responseContinuation.ResponseAsync.ContinueWith(
                    responseTask =>
                    {
                        try
                        {
                            var response = responseTask.Result;
                            rpcScope.RecordResponse(response);
                            rpcScope.Complete();
                            return response;
                        }
                        catch (AggregateException ex)
                        {
                            rpcScope.CompleteWithException(ex.InnerException);
                            throw ex.InnerException;
                        }
                    });

                return new AsyncClientStreamingCall<TRequest, TResponse>(
                    clientRequestStreamProxy,
                    responseAsync,
                    responseContinuation.ResponseHeadersAsync,
                    responseContinuation.GetStatus,
                    responseContinuation.GetTrailers,
                    responseContinuation.WithBestEffortDispose(rpcScope));
            }
            catch (Exception e)
            {
                rpcScope?.CompleteWithException(e);
                throw;
            }
            finally
            {
                rpcScope?.RestoreParentActivity();
            }
        }

        /// <inheritdoc/>
        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            ClientRpcScope<TRequest, TResponse> rpcScope = null;

            try
            {
                rpcScope = new ClientRpcScope<TRequest, TResponse>(context, this.options);
                rpcScope.RecordRequest(request);
                var responseContinuation = continuation(request, rpcScope.Context);

                var responseStreamProxy = new AsyncStreamReaderProxy<TResponse>(
                    responseContinuation.ResponseStream,
                    rpcScope.RecordResponse,
                    rpcScope.Complete,
                    rpcScope.CompleteWithException);

                return new AsyncServerStreamingCall<TResponse>(
                    responseStreamProxy,
                    responseContinuation.ResponseHeadersAsync,
                    responseContinuation.GetStatus,
                    responseContinuation.GetTrailers,
                    responseContinuation.WithBestEffortDispose(rpcScope));
            }
            catch (Exception e)
            {
                rpcScope?.CompleteWithException(e);
                throw;
            }
            finally
            {
                rpcScope?.RestoreParentActivity();
            }
        }

        /// <inheritdoc/>
        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            ClientRpcScope<TRequest, TResponse> rpcScope = null;

            try
            {
                rpcScope = new ClientRpcScope<TRequest, TResponse>(context, this.options);
                var responseContinuation = continuation(rpcScope.Context);

                var requestStreamProxy = new ClientStreamWriterProxy<TRequest>(
                    responseContinuation.RequestStream,
                    rpcScope.RecordRequest);

                var responseStreamProxy = new AsyncStreamReaderProxy<TResponse>(
                    responseContinuation.ResponseStream,
                    rpcScope.RecordResponse,
                    rpcScope.Complete,
                    rpcScope.CompleteWithException);

                return new AsyncDuplexStreamingCall<TRequest, TResponse>(
                    requestStreamProxy,
                    responseStreamProxy,
                    responseContinuation.ResponseHeadersAsync,
                    responseContinuation.GetStatus,
                    responseContinuation.GetTrailers,
                    responseContinuation.WithBestEffortDispose(rpcScope));
            }
            catch (Exception e)
            {
                rpcScope?.CompleteWithException(e);
                throw;
            }
            finally
            {
                rpcScope?.RestoreParentActivity();
            }
        }

        /// <summary>
        /// A class to help track the lifetime of a client-side RPC.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        private sealed class ClientRpcScope<TRequest, TResponse> : RpcScope<TRequest, TResponse>
            where TRequest : class
            where TResponse : class
        {
            /// <summary>
            /// The metadata setter action.
            /// </summary>
            private static readonly Action<Metadata, string, string> MetadataSetter = (metadata, key, value) => { metadata.Add(new Metadata.Entry(key, value)); };

            /// <summary>
            /// The context.
            /// </summary>
            private readonly ClientInterceptorContext<TRequest, TResponse> context;

            /// <summary>
            /// The parent activity.
            /// </summary>
            private readonly Activity parentActivity;

            /// <summary>
            /// Initializes a new instance of the <see cref="ClientRpcScope{TRequest, TResponse}" /> class.
            /// </summary>
            /// <param name="context">The context.</param>
            /// <param name="options">The options.</param>
            public ClientRpcScope(ClientInterceptorContext<TRequest, TResponse> context, ClientTracingInterceptorOptions options)
                : base(context.Method?.FullName, options.RecordMessageEvents)
            {
                this.context = context;

                // Capture the current activity.
                this.parentActivity = Activity.Current;

                // Short-circuit if nobody is listening
                if (!GrpcCoreInstrumentation.ActivitySource.HasListeners())
                {
                    return;
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

                // We want to start an activity but don't activate it.
                // After calling StartActivity, Activity.Current will be the new Activity.
                // This scope is created synchronously before the RPC invocation starts and so this new Activity will overwrite
                // the callers current Activity which isn't what we want. We need to restore the original immediately after doing this.
                // If this call happened after some kind of async context await then a restore wouldn't be necessary.
                // gRPC Core just doesn't have the hooks to do this as far as I can tell.
                var rpcActivity = GrpcCoreInstrumentation.ActivitySource.StartActivity(
                    this.FullServiceName,
                    ActivityKind.Client,
                    this.parentActivity == default ? default : this.parentActivity.Context,
                    tags: customTags);

                if (rpcActivity == null)
                {
                    return;
                }

                var callOptions = context.Options;

                if (callOptions.Headers == null)
                {
                    callOptions = callOptions.WithHeaders(new Metadata());
                }

                this.SetActivity(rpcActivity);
                options.Propagator.Inject(new PropagationContext(rpcActivity.Context, Baggage.Current), callOptions.Headers, MetadataSetter);
                this.context = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, callOptions);
            }

            /// <summary>
            /// Gets the context.
            /// </summary>
            public ClientInterceptorContext<TRequest, TResponse> Context => this.context;

            /// <summary>
            /// Restores the parent activity.
            /// </summary>
            public void RestoreParentActivity()
            {
                Activity.Current = this.parentActivity;
            }
        }
    }
}
