// <copyright file="AutoFlushActivityProcessor.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Threading;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Activity processor that flushes its containing <see cref="TracerProvider"/> if an ended
    /// activity matches a predicate.
    /// </summary>
    public sealed class AutoFlushActivityProcessor : BaseProcessor<Activity>
    {
        private readonly Predicate<Activity> predicate;
        private readonly int timeoutMilliseconds;

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoFlushActivityProcessor"/> class.
        /// </summary>
        /// <param name="predicate">Predicate that should return <c>true</c> to initiate a flush.</param>
        /// <param name="timeoutMilliseconds">Timeout (in milliseconds) to use for flushing.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the <c>timeoutMilliseconds</c> is smaller than -1.
        /// </exception>
        public AutoFlushActivityProcessor(Predicate<Activity> predicate, int timeoutMilliseconds)
        {
            this.predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            if (timeoutMilliseconds < Timeout.Infinite)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
            }

            this.timeoutMilliseconds = timeoutMilliseconds;
        }

        /// <inheritdoc/>
        public override void OnEnd(Activity data)
        {
            if (this.predicate(data))
            {
                (this.ParentProvider as TracerProvider)?.ForceFlush(this.timeoutMilliseconds);
            }
        }
    }
}
