// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Channels;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal sealed class MetricsChannel
{
    private readonly Channel<string> channel = Channel.CreateBounded<string>(new BoundedChannelOptions(10_000)
    {
        SingleReader = true,
        SingleWriter = false,
    });

    public ChannelReader<string> Reader => this.channel.Reader;

    public ChannelWriter<string> Writer => this.channel.Writer;
}
