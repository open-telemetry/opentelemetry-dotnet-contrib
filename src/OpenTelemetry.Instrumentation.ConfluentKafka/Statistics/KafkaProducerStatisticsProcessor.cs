// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// Adapted from Aspire.Confluent.Kafka's MetricsChannel and MetricsService.
// Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.
// See THIRD-PARTY-NOTICES.TXT for the original license and source attribution.

using System.Threading.Channels;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal sealed class KafkaProducerStatisticsProcessor : IDisposable
{
    private readonly Channel<string> channel;
    private readonly Action<string> update;
    private int disposed;

    public KafkaProducerStatisticsProcessor(Action<string> update, int capacity = 16)
    {
        this.update = update;
        this.channel = Channel.CreateBounded<string>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,

            // Only TryWrite is used: a full queue must never block the producer poll thread.
            FullMode = BoundedChannelFullMode.Wait,
        });
        this.Completion = Task.Run(this.ProcessAsync);
    }

    public Task Completion { get; }

    public bool TryEnqueue(string json)
    {
        if (Volatile.Read(ref this.disposed) != 0)
        {
            return false;
        }

        if (this.channel.Writer.TryWrite(json))
        {
            return true;
        }

        if (Volatile.Read(ref this.disposed) == 0)
        {
            ConfluentKafkaInstrumentationEventSource.Log.StatisticsQueueFull();
        }

        return false;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref this.disposed, 1);
        this.channel.Writer.TryComplete();
    }

    private async Task ProcessAsync()
    {
        while (await this.channel.Reader.WaitToReadAsync().ConfigureAwait(false))
        {
            while (this.channel.Reader.TryRead(out var json))
            {
                if (Volatile.Read(ref this.disposed) != 0)
                {
                    continue;
                }

                try
                {
                    this.update(json);
                }
                catch (Exception ex)
                {
                    ConfluentKafkaInstrumentationEventSource.Log.StatisticsHandlerFailed(ex);
                }
            }
        }
    }
}
