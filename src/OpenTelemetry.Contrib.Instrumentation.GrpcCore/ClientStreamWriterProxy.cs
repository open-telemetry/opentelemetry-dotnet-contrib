﻿// <copyright file="ClientStreamWriterProxy.cs" company="OpenTelemetry Authors">
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
    using System.Threading.Tasks;
    using global::Grpc.Core;

    /// <summary>
    /// A proxy client stream writer.
    /// </summary>
    /// <remarks>
    /// Borrowed heavily from
    /// https://github.com/opentracing-contrib/csharp-grpc/blob/master/src/OpenTracing.Contrib.Grpc/Streaming/TracingClientStreamWriter.cs.
    /// </remarks>
    /// <typeparam name="T">The message type.</typeparam>
    /// <seealso cref="global::Grpc.Core.IClientStreamWriter{T}" />
    internal class ClientStreamWriterProxy<T> : IClientStreamWriter<T>
    {
        /// <summary>
        /// The writer.
        /// </summary>
        private readonly IClientStreamWriter<T> writer;

        /// <summary>
        /// The on write action.
        /// </summary>
        private readonly Action<T> onWrite;

        /// <summary>
        /// The on complete action.
        /// </summary>
        private readonly Action onComplete;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientStreamWriterProxy{T}"/> class.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="onWrite">The on write action if any.</param>
        /// <param name="onComplete">The on complete action, if any.</param>
        public ClientStreamWriterProxy(IClientStreamWriter<T> writer, Action<T> onWrite = null, Action onComplete = null)
        {
            this.writer = writer;
            this.onWrite = onWrite;
            this.onComplete = onComplete;
        }

        /// <inheritdoc/>
        public WriteOptions WriteOptions
        {
            get => this.writer.WriteOptions;
            set => this.writer.WriteOptions = value;
        }

        /// <inheritdoc/>
        public Task WriteAsync(T message)
        {
            this.onWrite?.Invoke(message);
            return this.writer.WriteAsync(message);
        }

        /// <inheritdoc/>
        public Task CompleteAsync()
        {
            this.onComplete?.Invoke();
            return this.writer.CompleteAsync();
        }
    }
}
