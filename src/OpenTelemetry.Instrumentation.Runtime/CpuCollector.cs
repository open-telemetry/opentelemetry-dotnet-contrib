// <copyright file="CpuCollector.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.Runtime
{

    /// <summary>
    /// .NET cpu metrics collector
    /// </summary>
    internal class CpuCollector : IDisposable
    {
        private long TRIGER_PERIOD {get; set;} = 1000;

        private const short DUE_TIME = 1000;

        private readonly Process _process = Process.GetCurrentProcess();
        private System.Threading.Timer _timer;
        public static MetricsRecord Record { get; private set; } = new MetricsRecord();

        private DateTime _lastTimeStamp;
        private TimeSpan _lastTotalProcTime = TimeSpan.Zero;
        private TimeSpan _lastUserProcTime = TimeSpan.Zero;
        private TimeSpan _lastPrivilegedProcTime = TimeSpan.Zero;

        public struct MetricsRecord
        {
            public MetricsRecord(double totalCpuUsed, double privilegedCpuUsed, double userCpuUsed)
            {
                TotalCpuUsed = totalCpuUsed;

                PrivilegedCpuUsed = privilegedCpuUsed;

                UserCpuUsed = userCpuUsed;
            }

            public double TotalCpuUsed { get; internal set;}
            public double PrivilegedCpuUsed { get; internal set;}
            public double UserCpuUsed { get; internal set;}
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CpuCollector"/> class.
        /// </summary>
        public CpuCollector(RuntimeMetricsOptions options)
        {
            _lastTimeStamp = _process.StartTime;

            if(options.CpuCollectInterval.HasValue && options.CpuCollectInterval.Value < 1000){
                TRIGER_PERIOD = 1000;
            }

            _timer = new System.Threading.Timer(CollectData!, null, DUE_TIME, TRIGER_PERIOD);
        }

        /// <summary>
        /// Collects CPU metrics.
        /// </summary>
        private void CollectData(object state)
        {
            _process.Refresh();

            var totalCpuTimeUsed = _process.TotalProcessorTime.TotalMilliseconds - _lastTotalProcTime.TotalMilliseconds;
            var privilegedCpuTimeUsed = _process.PrivilegedProcessorTime.TotalMilliseconds - _lastPrivilegedProcTime.TotalMilliseconds;
            var userCpuTimeUsed = _process.UserProcessorTime.TotalMilliseconds - _lastUserProcTime.TotalMilliseconds;

            _lastTotalProcTime = _process.TotalProcessorTime;
            _lastPrivilegedProcTime = _process.PrivilegedProcessorTime;
            _lastUserProcTime = _process.UserProcessorTime;

            try
            {
                var cpuTimeElapsed = (DateTime.UtcNow - _lastTimeStamp).TotalMilliseconds * Environment.ProcessorCount;
                _lastTimeStamp = DateTime.UtcNow;

                var totla_cpu = totalCpuTimeUsed * 100 / cpuTimeElapsed;
                var privilaged_cpu = privilegedCpuTimeUsed * 100 / cpuTimeElapsed;
                var user_cpu = userCpuTimeUsed * 100 / cpuTimeElapsed;

                Record = new MetricsRecord()
                {
                    TotalCpuUsed = totalCpuTimeUsed * 100 / cpuTimeElapsed,
                    PrivilegedCpuUsed = privilegedCpuTimeUsed * 100 / cpuTimeElapsed,
                    UserCpuUsed = userCpuTimeUsed * 100 / cpuTimeElapsed,
                };

            }
            catch
            {

            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this._timer?.Dispose();
        }
    }
}
