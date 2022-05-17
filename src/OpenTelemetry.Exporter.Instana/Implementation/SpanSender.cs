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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("OpenTelemetry.Exporter.Instana.Tests, PublicKey=002400000480000094000000060200000024000052534131000400000100010051c1562a090fb0c9f391012a32198b5e5d9a60e9b80fa2d7b434c9e5ccb7259bd606e66f9660676afc6692b8cdc6793d190904551d2103b7b22fa636dcbb8208839785ba402ea08fc00c8f1500ccef28bbf599aa64ffb1e1d5dc1bf3420a3777badfe697856e9d52070a50c3ea5821c80bef17ca3acffa28f89dd413f096f898")]

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
            this.spansQueue.Enqueue(instanaSpan);
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
