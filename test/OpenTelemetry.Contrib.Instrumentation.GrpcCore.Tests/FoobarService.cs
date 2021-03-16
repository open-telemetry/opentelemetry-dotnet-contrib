﻿// <copyright file="FoobarService.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace OpenTelemetry.Contrib.Instrumentation.GrpcCore.Test
{
    /// <summary>
    /// Test implementation of foobar.
    /// </summary>
    internal class FoobarService : Foobar.FoobarBase
    {
        /// <summary>
        /// Default traceparent header value with the sampling bit on.
        /// </summary>
        internal static readonly string DefaultTraceparentWithSampling = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

        /// <summary>
        /// The default parent from a traceparent header.
        /// </summary>
        internal static readonly ActivityContext DefaultParentFromTraceparentHeader = ActivityContext.Parse(DefaultTraceparentWithSampling, null);

        /// <summary>
        /// The default request message.
        /// </summary>
        internal static readonly FoobarRequest DefaultRequestMessage = new FoobarRequest { Message = "foo" };

        /// <summary>
        /// The default request message size.
        /// </summary>
        internal static readonly int DefaultRequestMessageSize = ((IMessage)DefaultRequestMessage).CalculateSize();

        /// <summary>
        /// The default response message.
        /// </summary>
        internal static readonly FoobarResponse DefaultResponseMessage = new FoobarResponse { Message = "bar" };

        /// <summary>
        /// The default request message size.
        /// </summary>
        internal static readonly int DefaultResponseMessageSize = ((IMessage)DefaultResponseMessage).CalculateSize();

        /// <summary>
        /// The request header fail with status code.
        /// </summary>
        internal static readonly string RequestHeaderFailWithStatusCode = "failurestatuscode";

        /// <summary>
        /// The request header error description.
        /// </summary>
        internal static readonly string RequestHeaderErrorDescription = "failuredescription";

        /// <summary>
        /// Starts the specified service.
        /// </summary>
        /// <param name="serverInterceptor">The server interceptor.</param>
        /// <returns>A tuple.</returns>
        public static DisposableServer Start(Interceptor serverInterceptor = null)
        {
            // Disable SO_REUSEPORT to prevent https://github.com/grpc/grpc/issues/10755
            var serviceDefinition = Foobar.BindService(new FoobarService());
            if (serverInterceptor != null)
            {
                serviceDefinition = serviceDefinition.Intercept(serverInterceptor);
            }

            var server = new Server
            {
                Ports = { { "localhost", ServerPort.PickUnused, ServerCredentials.Insecure } },
                Services = { serviceDefinition },
            };

            server.Start();
            var serverUriString = new Uri("dns:localhost:" + server.Ports.Single().BoundPort).ToString();

            return new DisposableServer(server, serverUriString);
        }

        /// <summary>
        /// Builds the default RPC client.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="clientTracingInterceptor">The client tracing interceptor, if any.</param>
        /// <param name="additionalMetadata">The additional metadata, if any.</param>
        /// <returns>
        /// The gRPC client.
        /// </returns>
        public static Foobar.FoobarClient ConstructRpcClient(
            string target,
            ClientTracingInterceptor clientTracingInterceptor = null,
            IEnumerable<Metadata.Entry> additionalMetadata = null)
        {
            var channel = new Channel(target, ChannelCredentials.Insecure);
            var callInvoker = channel.CreateCallInvoker();

            if (clientTracingInterceptor != null)
            {
                callInvoker = callInvoker.Intercept(clientTracingInterceptor);
            }

            // The metadata injector comes first
            if (additionalMetadata != null)
            {
                callInvoker = callInvoker.Intercept(
                    metadata =>
                    {
                        foreach (var m in additionalMetadata)
                        {
                            metadata.Add(m);
                        }

                        return metadata;
                    });
            }

            return new Foobar.FoobarClient(callInvoker);
        }

        /// <summary>
        /// Makes a unary asynchronous request.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <returns>A Task.</returns>
        public static async Task MakeUnaryAsyncRequest(Foobar.FoobarClient client)
        {
            using var call = client.UnaryAsync(DefaultRequestMessage);
            _ = await call.ResponseAsync.ConfigureAwait(false);
        }

        /// <summary>
        /// Makes a client streaming request.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <returns>A Task.</returns>
        public static async Task MakeClientStreamingRequest(Foobar.FoobarClient client)
        {
            using var call = client.ClientStreaming();
            await call.RequestStream.WriteAsync(DefaultRequestMessage).ConfigureAwait(false);
            await call.RequestStream.CompleteAsync().ConfigureAwait(false);
            _ = await call.ResponseAsync.ConfigureAwait(false);
        }

        /// <summary>
        /// Makes a server streaming request.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <returns>A Task.</returns>
        public static async Task MakeServerStreamingRequest(Foobar.FoobarClient client)
        {
            using var call = client.ServerStreaming(DefaultRequestMessage);
            while (await call.ResponseStream.MoveNext().ConfigureAwait(false))
            {
            }
        }

        /// <summary>
        /// Makes a duplex streaming request.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <returns>A Task.</returns>
        public static async Task MakeDuplexStreamingRequest(Foobar.FoobarClient client)
        {
            using var call = client.DuplexStreaming();
            await call.RequestStream.WriteAsync(DefaultRequestMessage).ConfigureAwait(false);
            await call.RequestStream.CompleteAsync().ConfigureAwait(false);

            while (await call.ResponseStream.MoveNext().ConfigureAwait(false))
            {
            }
        }

        /// <inheritdoc/>
        public override Task<FoobarResponse> Unary(FoobarRequest request, ServerCallContext context)
        {
            this.CheckForFailure(context);

            return Task.FromResult(DefaultResponseMessage);
        }

        /// <inheritdoc/>
        public override async Task<FoobarResponse> ClientStreaming(IAsyncStreamReader<FoobarRequest> requestStream, ServerCallContext context)
        {
            this.CheckForFailure(context);

            while (await requestStream.MoveNext().ConfigureAwait(false))
            {
            }

            return DefaultResponseMessage;
        }

        /// <inheritdoc/>
        public override async Task ServerStreaming(FoobarRequest request, IServerStreamWriter<FoobarResponse> responseStream, ServerCallContext context)
        {
            this.CheckForFailure(context);

            await responseStream.WriteAsync(DefaultResponseMessage).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override async Task DuplexStreaming(IAsyncStreamReader<FoobarRequest> requestStream, IServerStreamWriter<FoobarResponse> responseStream, ServerCallContext context)
        {
            this.CheckForFailure(context);

            while (await requestStream.MoveNext().ConfigureAwait(false))
            {
            }

            await responseStream.WriteAsync(DefaultResponseMessage).ConfigureAwait(false);
        }

        /// <summary>
        /// Throws if we see some by-convention request metadata.
        /// </summary>
        /// <param name="context">The context.</param>
        private void CheckForFailure(ServerCallContext context)
        {
            var failureStatusCodeString = context.RequestHeaders.GetValue(RequestHeaderFailWithStatusCode);
            var failureDescription = context.RequestHeaders.GetValue(RequestHeaderErrorDescription);
            if (failureStatusCodeString != null)
            {
                throw new RpcException(new Status((StatusCode)Enum.Parse(typeof(StatusCode), failureStatusCodeString), failureDescription ?? string.Empty));
            }
        }

        /// <summary>
        /// Wraps server shutdown with an IDisposable pattern.
        /// </summary>
        /// <seealso cref="System.IDisposable" />
        public sealed class DisposableServer : IDisposable
        {
            /// <summary>
            /// The server.
            /// </summary>
            private readonly Server server;

            /// <summary>
            /// Initializes a new instance of the <see cref="DisposableServer" /> class.
            /// </summary>
            /// <param name="server">The server.</param>
            /// <param name="uriString">The URI string.</param>
            public DisposableServer(Server server, string uriString)
            {
                this.server = server;
                this.UriString = uriString;
            }

            /// <summary>
            /// Gets the URI string.
            /// </summary>
            public string UriString { get; }

            /// <inheritdoc/>
            public void Dispose()
            {
                this.server.ShutdownAsync().Wait();
            }
        }
    }
}
