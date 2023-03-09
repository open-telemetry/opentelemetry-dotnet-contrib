// <copyright file="ServerStreamWriterProxy.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using Grpc.Core;

namespace OpenTelemetry.Instrumentation.GrpcCore;

/// <summary>
/// A proxy server stream writer.
/// </summary>
/// <remarks>
/// Borrowed heavily from
/// https://github.com/opentracing-contrib/csharp-grpc/blob/master/src/OpenTracing.Contrib.Grpc/Streaming/TracingServerStreamWriter.cs.
/// </remarks>
/// <typeparam name="T">The message type.</typeparam>
/// <seealso cref="IServerStreamWriter{T}" />
internal class ServerStreamWriterProxy<T> : IServerStreamWriter<T>
{
    /// <summary>
    /// The writer.
    /// </summary>
    private readonly IServerStreamWriter<T> writer;

    /// <summary>
    /// The on write action.
    /// </summary>
    private readonly Action<T> onWrite;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerStreamWriterProxy{T}"/> class.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="onWrite">The on write action, if any.</param>
    public ServerStreamWriterProxy(IServerStreamWriter<T> writer, Action<T> onWrite = null)
    {
        this.writer = writer;
        this.onWrite = onWrite;
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
}
