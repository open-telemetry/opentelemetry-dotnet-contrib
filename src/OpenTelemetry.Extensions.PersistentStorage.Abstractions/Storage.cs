// <copyright file="Storage.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;

namespace OpenTelemetry.Extensions.PersistentStorage.Abstractions
{
    /// <summary>
    /// Represents persistent storage.
    /// </summary>
    public abstract class Storage
    {
        /// <summary>
        /// Reads a sequence of blobs from storage.
        /// </summary>
        /// <param name="blobs">
        /// list of blobs if found.
        /// </param>
        /// <returns>
        /// Sequence of blobs from storage.
        /// </returns>
        /// <remarks>
        /// Note to implementers: This function should never throw exception.
        /// </remarks>
        public abstract bool TryGetBlobs(out IEnumerable<Blob> blobs);

        /// <summary>
        /// Attempts to get a blob from storage.
        /// </summary>
        /// <param name="blob">
        /// Blob object if found.
        /// </param>
        /// <returns>
        /// A blob if there is an available one, or null if there is no blob available.
        /// </returns>
        /// <remarks>
        /// Note to implementers: This function should never throw exception.
        /// </remarks>
        public abstract bool TryGetBlob(out Blob blob);

        /// <summary>
        /// Creates a new blob with the provided data.
        /// </summary>
        /// <param name="blob">
        /// Created blob.
        /// </param>
        /// <param name="buffer">
        /// The content to be written.
        /// </param>
        /// <param name="leasePeriodMilliseconds">
        /// The number of milliseconds to lease after the blob is created.
        /// </param>
        /// <returns>
        /// True if the blob was created or else false.
        /// </returns>
        /// <remarks>
        /// Note to implementers: This function should never throw exception.
        /// </remarks>
        public abstract bool TryCreateBlob(out Blob blob, byte[] buffer, int leasePeriodMilliseconds = 0);
    }
}
