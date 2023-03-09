// <copyright file="SpanSender.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace OpenTelemetry.Exporter.Instana.Implementation;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
internal sealed class SpanSender : ISpanSender
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
{
    private readonly Task queueSenderTask;
    private readonly ConcurrentQueue<InstanaSpan> spansQueue = new ConcurrentQueue<InstanaSpan>();

    public SpanSender()
    {
        // create a task that will send a batch of spans every second at least
        this.queueSenderTask = new Task(this.TaskSpanSender, TaskCreationOptions.LongRunning);
        this.queueSenderTask.Start();
    }

    public void Enqueue(InstanaSpan instanaSpan)
    {
        if (Transport.IsAvailable)
        {
            this.spansQueue.Enqueue(instanaSpan);
        }
    }

    private async void TaskSpanSender()
    {
        // this will be an infinite loop
        while (true)
        {
            // check if we can send spans
            if (this.spansQueue.TryPeek(out InstanaSpan _))
            {
                // actually send spans
                await Transport.SendSpansAsync(this.spansQueue).ConfigureAwait(false);
            }

            // rest for a while
            await Task.Delay(1000).ConfigureAwait(false);
        }
    }
}
