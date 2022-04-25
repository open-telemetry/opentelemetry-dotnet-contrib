// <copyright file="Blob.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Extensions.PersistentStorage.Abstractions
{
    /// <summary>
    /// Represents a persistent blob.
    /// </summary>
    public abstract class Blob
    {
        /// <summary>
        /// Attempts to read the content from the blob.
        /// </summary>
        /// <param name="buffer">
        /// The content to be read.
        /// </param>
        /// <returns>
        /// True if read was successful or else false.
        /// </returns>
        /// <remarks>
        /// Note to implementers: This function should never throw exception.
        /// </remarks>
        public abstract bool TryRead(out byte[] buffer);

        /// <summary>
        /// Attempts to write the given content to the blob.
        /// </summary>
        /// <param name="buffer">
        /// The content to be written.
        /// </param>
        /// <param name="leasePeriodMilliseconds">
        /// The number of milliseconds to lease after the write operation finished.
        /// </param>
        /// <returns>
        /// True if the write operation succeeded or else false.
        /// </returns>
        /// <remarks>
        /// Note to implementers: This function should never throw exception.
        /// </remarks>
        public abstract bool TryWrite(byte[] buffer, int leasePeriodMilliseconds = 0);

        /// <summary>
        /// Attempts to acquire lease on the blob.
        /// </summary>
        /// <param name="leasePeriodMilliseconds">
        /// The number of milliseconds to lease.
        /// </param>
        /// <returns>
        /// true if lease is acquired or else false.
        /// </returns>
        /// <remarks>
        /// Note to implementers: This function should never throw exception.
        /// </remarks>
        public abstract bool TryLease(int leasePeriodMilliseconds);

        /// <summary>
        /// Attempts to delete the blob.
        /// </summary>
        /// <returns>
        /// True if delete was successful else false.
        /// </returns>
        /// <remarks>
        /// Note to implementers: This function should never throw exception.
        /// </remarks>
        public abstract bool TryDelete();
    }
}
