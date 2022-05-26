// <copyright file="ActivityEventAttachingLogProcessorTests.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using Xunit;

namespace OpenTelemetry.Extensions.Tests
{
    public sealed class ActivityEventAttachingLogProcessorTests : IDisposable
    {
        private readonly ActivitySource activitySource = new ActivitySource("Test");
        private readonly ActivityListener activityListener = new ActivityListener
        {
            ShouldListenTo = source => true,
        };

        private readonly ILogger logger;
        private readonly ILoggerFactory loggerFactory;
        private OpenTelemetryLoggerOptions options;
        private bool sampled;

        public ActivityEventAttachingLogProcessorTests()
        {
            this.activityListener.Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
            {
                return this.sampled
                    ? ActivitySamplingResult.AllDataAndRecorded
                    : ActivitySamplingResult.PropagationData;
            };

            ActivitySource.AddActivityListener(this.activityListener);

            this.loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    this.options = options;
                    options.AttachLogsToActivityEvent();
                });
                builder.AddFilter(typeof(ActivityEventAttachingLogProcessorTests).FullName, LogLevel.Trace);
            });

            this.logger = this.loggerFactory.CreateLogger<ActivityEventAttachingLogProcessorTests>();
        }

        public void Dispose()
        {
            this.activitySource.Dispose();
            this.activityListener.Dispose();
            this.loggerFactory.Dispose();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [InlineData(true, 18, true, true, true)]
        [InlineData(true, 0, false, false, true, true)]
        public void AttachLogsToActivityEventTest(
            bool sampled,
            int eventId = 0,
            bool includeFormattedMessage = false,
            bool parseStateValues = false,
            bool includeScopes = false,
            bool recordException = false)
        {
            this.sampled = sampled;
            this.options.IncludeFormattedMessage = includeFormattedMessage;
            this.options.ParseStateValues = parseStateValues;
            this.options.IncludeScopes = includeScopes;

            Activity activity = this.activitySource.StartActivity("Test");

            using IDisposable scope = this.logger.BeginScope("{NodeId}", 99);

            this.logger.LogInformation(eventId, "Hello OpenTelemetry {UserId}!", 8);

            Activity innerActivity = null;
            if (recordException)
            {
                innerActivity = this.activitySource.StartActivity("InnerTest");

                using IDisposable innerScope = this.logger.BeginScope("{RequestId}", "1234");

                this.logger.LogError(new InvalidOperationException("Goodbye OpenTelemetry."), "Exception event.");

                innerActivity.Dispose();
            }

            activity.Dispose();

            if (sampled)
            {
                ActivityEvent? logEvent = activity.Events.FirstOrDefault();

                Assert.NotNull(logEvent);
                Assert.Equal("log", logEvent.Value.Name);

                Dictionary<string, object> tags = logEvent.Value.Tags?.ToDictionary(i => i.Key, i => i.Value);
                Assert.NotNull(tags);

                Assert.Equal("OpenTelemetry.Extensions.Tests.ActivityEventAttachingLogProcessorTests", tags[nameof(LogRecord.CategoryName)]);
                Assert.Equal(LogLevel.Information, tags[nameof(LogRecord.LogLevel)]);

                if (eventId != 0)
                {
                    Assert.Equal((EventId)eventId, tags[nameof(LogRecord.EventId)]);
                }
                else
                {
                    Assert.DoesNotContain(tags, kvp => kvp.Key == nameof(LogRecord.EventId));
                }

                if (includeFormattedMessage)
                {
                    Assert.Equal("Hello OpenTelemetry 8!", tags[nameof(LogRecord.FormattedMessage)]);
                }
                else
                {
                    Assert.DoesNotContain(tags, kvp => kvp.Key == nameof(LogRecord.FormattedMessage));
                }

                if (parseStateValues)
                {
                    Assert.Equal(8, tags["state.UserId"]);
                }
                else
                {
                    Assert.DoesNotContain(tags, kvp => kvp.Key == "state.UserId");
                }

                if (includeScopes)
                {
                    Assert.Equal(99, tags["scope[0].NodeId"]);
                }
                else
                {
                    Assert.DoesNotContain(tags, kvp => kvp.Key == "scope[0].NodeId");
                }

                if (recordException)
                {
                    ActivityEvent? exLogEvent = innerActivity.Events.FirstOrDefault();

                    Assert.NotNull(exLogEvent);
                    Assert.Equal("log", exLogEvent.Value.Name);

                    Dictionary<string, object> exLogTags = exLogEvent.Value.Tags?.ToDictionary(i => i.Key, i => i.Value);
                    Assert.NotNull(exLogTags);

                    Assert.Equal(99, exLogTags["scope[0].NodeId"]);
                    Assert.Equal("1234", exLogTags["scope[1].RequestId"]);

                    ActivityEvent? exEvent = innerActivity.Events.Skip(1).FirstOrDefault();

                    Assert.NotNull(exEvent);
                    Assert.Equal("exception", exEvent.Value.Name);

                    Dictionary<string, object> exTags = exEvent.Value.Tags?.ToDictionary(i => i.Key, i => i.Value);
                    Assert.NotNull(exTags);

                    Assert.Equal("System.InvalidOperationException", exTags["exception.type"]);
                    Assert.Equal("Goodbye OpenTelemetry.", exTags["exception.message"]);
                    Assert.Contains(exTags, kvp => kvp.Key == "exception.stacktrace");
                }
            }
            else
            {
                Assert.Empty(activity.Events);
            }
        }
    }
}
