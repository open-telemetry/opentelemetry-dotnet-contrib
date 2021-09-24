﻿// <copyright file="ActivityEventAttachingLogProcessor.cs" company="OpenTelemetry Authors">
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

#if NET461_OR_GREATER || NETSTANDARD2_0 || NET5_0_OR_GREATER
using System;
using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Logs
{
    internal sealed class ActivityEventAttachingLogProcessor : BaseProcessor<LogRecord>
    {
        private static readonly Action<LogRecordScope, State> ProcessScope = (LogRecordScope scope, State state) =>
        {
            try
            {
                state.Processor.options.ScopeConverter?.Invoke(state.Tags, state.Depth++, scope);
            }
            catch (Exception ex)
            {
                OpenTelemetryExtensionsEventSource.Log.LogProcessorException($"Processing scope of type [{scope.GetType().FullName}]", ex);
            }
        };

        private readonly LogToActivityEventConversionOptions options;

        public ActivityEventAttachingLogProcessor(LogToActivityEventConversionOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public override void OnEnd(LogRecord data)
        {
            Activity? activity = Activity.Current;

            if (activity?.IsAllDataRequested == true)
            {
                var tags = new ActivityTagsCollection
                {
                    { nameof(data.CategoryName), data.CategoryName },
                    { nameof(data.LogLevel), data.LogLevel },
                };

                if (data.EventId != 0)
                {
                    tags[nameof(data.EventId)] = data.EventId;
                }

                var activityEvent = new ActivityEvent("log", data.Timestamp, tags);

                data.ForEachScope(ProcessScope, new State(tags, this));

                if (data.StateValues != null)
                {
                    try
                    {
                        this.options.StateConverter?.Invoke(tags, data.StateValues);
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        OpenTelemetryExtensionsEventSource.Log.LogProcessorException($"Processing state of type [{data.State.GetType().FullName}]", ex);
                    }
                }

                if (!string.IsNullOrEmpty(data.FormattedMessage))
                {
                    tags[nameof(data.FormattedMessage)] = data.FormattedMessage;
                }

                activity.AddEvent(activityEvent);

                if (data.Exception != null)
                {
                    activity.RecordException(data.Exception);
                }
            }
        }

        private class State
        {
            public State(ActivityTagsCollection tags, ActivityEventAttachingLogProcessor processor)
            {
                this.Tags = tags;
                this.Processor = processor;
            }

            public ActivityTagsCollection Tags { get; }

            public ActivityEventAttachingLogProcessor Processor { get; }

            public int Depth { get; set; }
        }
    }
}
#endif
