// <copyright file="TagName.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Contrib.Instrumentation.Quartz.Implementation
{
    internal static class TagName
    {
        public const string DefaultListenerName = "Quartz";
        public const string SchedulerName = "scheduler.name";
        public const string SchedulerId = "scheduler.id";
        public const string FireInstanceId = "fire.instance.id";
        public const string TriggerGroup = "trigger.group";
        public const string TriggerName = "trigger.name";
        public const string JobType = "job.type";
        public const string JobGroup = "job.group";
        public const string JobName = "job.name";
    }
}
