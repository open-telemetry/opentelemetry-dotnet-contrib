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

namespace OpenTelemetry.Exporter.Instana.Implementation
{
    internal class SpanSender : ISpanSender
    {
        private readonly Task queueSenderTask;
        private readonly Transport transport = new Transport();
        private readonly ConcurrentQueue<InstanaSpan> spansQueue = new ConcurrentQueue<InstanaSpan>();

        public SpanSender()
        {
            // create a task that will send a batch of spans every second at least
            this.queueSenderTask = new Task(this.TaskSpanSender, TaskCreationOptions.LongRunning);
            this.queueSenderTask.Start();
        }

        public void Enqueue(InstanaSpan instanaSpan)
        {
            if (this.transport.IsAvailable)
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
                if (this.spansQueue.TryPeek(out InstanaSpan dummySpan))
                {
                    // actually send spans
                    await this.transport.SendSpansAsync(this.spansQueue);
                }

                // rest for a while
                await Task.Delay(1000);
            }
        }
    }
}
