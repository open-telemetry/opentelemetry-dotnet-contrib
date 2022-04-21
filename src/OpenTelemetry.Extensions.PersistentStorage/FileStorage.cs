﻿// <copyright file="FileStorage.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Extensions.PersistentStorage
{
    /// <summary>
    /// Persistent file storage <see cref="FileStorage"/> allows to save data
    /// as blobs in file storage.
    /// </summary>
    public class FileStorage : IPersistentStorage, IDisposable
    {
        private readonly string directoryPath;
        private readonly long maxSizeInBytes;
        private readonly long retentionPeriodInMilliseconds;
        private readonly int writeTimeoutInMilliseconds;
        private readonly Timer maintenanceTimer;
        private bool disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileStorage"/>
        /// class.
        /// </summary>
        /// <param name="path">
        /// Sets file storage folder location where blobs are stored.
        /// </param>
        /// <param name="maxSizeInBytes">
        /// Maximum allowed storage folder size.
        /// Default is 50 MB.
        /// </param>
        /// <param name="maintenancePeriodInMilliseconds">
        /// Maintenance event runs at specified interval.
        /// Removes expired leases and blobs that exceed retention period.
        /// Default is 2 minutes.
        /// </param>
        /// <param name="retentionPeriodInMilliseconds">
        /// Retention period in milliseconds for the blob.
        /// Default is 2 days.
        /// </param>
        /// <param name="writeTimeoutInMilliseconds">
        /// Controls the timeout when writing a buffer to blob.
        /// Default is 1 minute.
        /// </param>
        public FileStorage(
            string path,
            long maxSizeInBytes = 52428800,
            int maintenancePeriodInMilliseconds = 120000,
            long retentionPeriodInMilliseconds = 172800000,
            int writeTimeoutInMilliseconds = 60000)
        {
            Guard.ThrowIfNull(path);

            // TODO: Validate time period values
            this.directoryPath = PersistentStorageHelper.CreateSubdirectory(path);
            this.maxSizeInBytes = maxSizeInBytes;
            this.retentionPeriodInMilliseconds = retentionPeriodInMilliseconds;
            this.writeTimeoutInMilliseconds = writeTimeoutInMilliseconds;

            this.maintenanceTimer = new Timer(maintenancePeriodInMilliseconds);
            this.maintenanceTimer.Elapsed += this.OnMaintenanceEvent;
            this.maintenanceTimer.AutoReset = true;
            this.maintenanceTimer.Enabled = true;
        }

        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.maintenanceTimer.Dispose();
                }

                this.disposedValue = true;
            }
        }

        /// <inheritdoc/>
        public IEnumerable<IPersistentBlob> GetBlobs()
        {
            var retentionDeadline = DateTime.UtcNow - TimeSpan.FromMilliseconds(this.retentionPeriodInMilliseconds);

            foreach (var file in Directory.EnumerateFiles(this.directoryPath, "*.blob", SearchOption.TopDirectoryOnly).OrderByDescending(f => f))
            {
                DateTime fileDateTime = PersistentStorageHelper.GetDateTimeFromBlobName(file);
                if (fileDateTime > retentionDeadline)
                {
                    yield return new FileBlob(file);
                }
            }
        }

        /// <inheritdoc/>
        public IPersistentBlob GetBlob()
        {
            return this.GetBlobs().FirstOrDefault();
        }

        /// <inheritdoc/>
        public IPersistentBlob CreateBlob(byte[] buffer, int leasePeriodMilliseconds = 0)
        {
            if (!this.CheckStorageSize())
            {
                return null;
            }

            try
            {
                var blobFilePath = Path.Combine(this.directoryPath, PersistentStorageHelper.GetUniqueFileName(".blob"));
                var blob = new FileBlob(blobFilePath);
                return blob.Write(buffer, leasePeriodMilliseconds);
            }
            catch (Exception ex)
            {
                PersistentStorageEventSource.Log.Warning("CreateBlob has failed.", ex);
                return null;
            }
        }

        private void OnMaintenanceEvent(object source, ElapsedEventArgs e)
        {
            try
            {
                if (!Directory.Exists(this.directoryPath))
                {
                    Directory.CreateDirectory(this.directoryPath);
                }
            }
            catch (Exception ex)
            {
                PersistentStorageEventSource.Log.Error($"Error creating directory {this.directoryPath}", ex);
                return;
            }

            PersistentStorageHelper.RemoveExpiredBlobs(this.directoryPath, this.retentionPeriodInMilliseconds, this.writeTimeoutInMilliseconds);
        }

        private bool CheckStorageSize()
        {
            var size = PersistentStorageHelper.GetDirectorySize();
            if (size >= this.maxSizeInBytes)
            {
                // TODO: check accuracy of size reporting.
                PersistentStorageEventSource.Log.Warning($"Persistent storage max capacity has been reached. Currently at {size / 1024} KB. " +
                                                "Please consider increasing the value of storage max size in exporter config.");
                return false;
            }

            return true;
        }
    }
}
