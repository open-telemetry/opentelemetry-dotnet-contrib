// <copyright file="ServiceConnectDiagnosticListener.cs" company="OpenTelemetry Authors">
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
using System.Text;
using OpenTelemetry.Trace;
using ServiceConnect.Interfaces;

namespace OpenTelemetry.Instrumentation.ServiceConnect.Implementation;

internal sealed class ServiceConnectDiagnosticListener : ListenerHandler
{
    public const string DiagnosticSourceName = "ServiceConnect.Bus";

    public const string ServiceConnectStartPublishCommand = "ServiceConnect.Bus.StartPublish";
    public const string ServiceConnectStopPublishCommand = "ServiceConnect.Bus.StopPublish";
    public const string ServiceConnectStartConsumeCommand = "ServiceConnect.Bus.StartConsume";
    public const string ServiceConnectStopConsumeCommand = "ServiceConnect.Bus.StopConsume";
    public const string ServiceConnectStartSendCommand = "ServiceConnect.Bus.StartSend";
    public const string ServiceConnectStopSendCommand = "ServiceConnect.Bus.StopSend";

    internal static readonly string ActivitySourceName = typeof(ServiceConnectDiagnosticListener).Assembly.GetName().Name ?? "ServiceConnect";
    internal static readonly string ActivityName = ActivitySourceName + ".Bus";

    internal static readonly Version? Version = typeof(ServiceConnectDiagnosticListener).Assembly.GetName().Version;
    internal static readonly ActivitySource ServiceConnectSource = new(ActivitySourceName, Version?.ToString() ?? "0.0.0");

    private const string AttributeDestinationAnonymous = "messaging.destination.anonymous";

    private readonly PropertyFetcher<string> routingKeyFetcher = new("RoutingKey");
    private readonly PropertyFetcher<Dictionary<string, string>?> stringHeadersFetcher = new("Headers");
    private readonly PropertyFetcher<byte[]> messageFetcher = new("Message");
    private readonly PropertyFetcher<Message> genericMessageFetcher = new("Message");
    private readonly PropertyFetcher<IDictionary<string, object>> consumeHeadersFetcher = new("Headers");
    private readonly PropertyFetcher<string> endPointFetcher = new("EndPoint");

    private readonly ServiceConnectInstrumentationOptions options;

    public ServiceConnectDiagnosticListener(string sourceName, ServiceConnectInstrumentationOptions? options)
        : base(sourceName)
    {
        this.options = options ?? new ServiceConnectInstrumentationOptions();
    }

    public override bool SupportsNullActivity => true;

    public override void OnCustom(string name, Activity? activity, object? payload)
    {
        var operation = string.Empty;

        switch (name)
        {
            case ServiceConnectStartPublishCommand:
                activity = ServiceConnectSource.StartActivity(
                    ActivityName + ".Publish",
                    ActivityKind.Producer,
                    default(ActivityContext));

                if (activity is null)
                {
                    ServiceConnectInstrumentationEventSource.Log.NullActivity(name);
                    return;
                }

                operation = "publish";
                _ = this.routingKeyFetcher.TryFetch(payload, out string publishRoutingKey);

                activity.DisplayName = (publishRoutingKey ?? "anonymous") + " " + operation;

                _ = activity.SetTag(SemanticConventions.AttributeMessagingSystem, "rabbitmq");
                _ = activity.SetTag(SemanticConventions.AttributeMessagingProtocol, "amqp");
                _ = activity.SetTag(SemanticConventions.AttributeMessagingOperation, operation);

                _ = this.genericMessageFetcher.TryFetch(payload, out Message? publishMessage);
                if (publishMessage is not null)
                {
                    _ = activity.SetTag(SemanticConventions.AttributeMessagingConversationId, publishMessage.CorrelationId.ToString());
                    try
                    {
                        this.options.EnrichWithMessage?.Invoke(activity, publishMessage);
                    }
                    catch (Exception ex)
                    {
                        ServiceConnectInstrumentationEventSource.Log.EnrichmentException(name, ex);
                    }
                }

                if (!string.IsNullOrEmpty(publishRoutingKey))
                {
                    _ = activity.SetTag(SemanticConventions.AttributeMessagingDestination, publishRoutingKey);
                }
                else
                {
                    _ = activity.SetTag(AttributeDestinationAnonymous, "true");
                }

                _ = this.stringHeadersFetcher.TryFetch(payload, out Dictionary<string, string>? publishHeaders);
                if (publishHeaders is null)
                {
                    break;
                }

                if (publishHeaders.TryGetValue("MessageId", out string? messageId))
                {
                    _ = activity.SetTag(SemanticConventions.AttributeMessagingMessageId, messageId);
                }

                break;

            case ServiceConnectStartConsumeCommand:
                activity = ServiceConnectSource.StartActivity(
                    ActivityName + ".Consume",
                    ActivityKind.Consumer,
                    default(ActivityContext));

                if (activity is null)
                {
                    ServiceConnectInstrumentationEventSource.Log.NullActivity(name);
                    return;
                }

                IDictionary<string, object> consumeHeaders = this.consumeHeadersFetcher.Fetch(payload);

                Dictionary<string, string?> readableHeaders = new();
                foreach (var kvp in consumeHeaders.ToList())
                {
                    if (kvp.Value.GetType() == typeof(byte[]))
                    {
                        readableHeaders[kvp.Key] = Encoding.UTF8.GetString((byte[])kvp.Value);
                        continue;
                    }

                    readableHeaders[kvp.Key] = kvp.Value.ToString();
                }

                operation = "receive";
                _ = readableHeaders.TryGetValue("DestinationAddress", out string? destinationAddress);
                activity.DisplayName = (destinationAddress ?? "anonymous") + " " + operation;

                _ = activity.SetTag(SemanticConventions.AttributeMessagingSystem, "rabbitmq");
                _ = activity.SetTag(SemanticConventions.AttributeMessagingProtocol, "amqp");
                _ = activity.SetTag(SemanticConventions.AttributeMessagingOperation, operation);
                _ = activity.SetTag(SemanticConventions.AttributeMessagingMessageId, readableHeaders["MessageId"]);
                if (!string.IsNullOrEmpty(destinationAddress))
                {
                    _ = activity.SetTag(SemanticConventions.AttributeMessagingDestination, destinationAddress);
                }
                else
                {
                    _ = activity.SetTag(AttributeDestinationAnonymous, "true");
                }

                byte[] consumeMessage = this.messageFetcher.Fetch(payload);
                _ = activity.SetTag(SemanticConventions.AttributeMessagingPayloadSize, consumeMessage.Length);
                try
                {
                    this.options.EnrichWithMessageBytes?.Invoke(activity, consumeMessage);
                }
                catch (Exception ex)
                {
                    ServiceConnectInstrumentationEventSource.Log.EnrichmentException(name, ex);
                }

                break;

            case ServiceConnectStartSendCommand:
                activity = ServiceConnectSource.StartActivity(
                    ActivityName + ".Send",
                    ActivityKind.Producer,
                    default(ActivityContext));

                if (activity is null)
                {
                    ServiceConnectInstrumentationEventSource.Log.NullActivity(name);
                    return;
                }

                _ = this.endPointFetcher.TryFetch(payload, out var endPoint);
                operation = "publish";
                activity.DisplayName = (endPoint ?? "anonymous") + " " + operation;

                _ = activity.SetTag(SemanticConventions.AttributeMessagingSystem, "rabbitmq");
                _ = activity.SetTag(SemanticConventions.AttributeMessagingProtocol, "amqp");
                _ = activity.SetTag(SemanticConventions.AttributeMessagingOperation, operation);
                if (!string.IsNullOrEmpty(endPoint))
                {
                    _ = activity.SetTag(SemanticConventions.AttributeMessagingDestination, endPoint);
                }
                else
                {
                    _ = activity.SetTag(AttributeDestinationAnonymous, "true");
                }

                _ = this.genericMessageFetcher.TryFetch(payload, out Message? sendMessage);
                if (sendMessage is null)
                {
                    break;
                }

                _ = activity.SetTag(SemanticConventions.AttributeMessagingConversationId, sendMessage.CorrelationId.ToString());
                try
                {
                    this.options.EnrichWithMessage?.Invoke(activity, sendMessage);
                }
                catch (Exception ex)
                {
                    ServiceConnectInstrumentationEventSource.Log.EnrichmentException(name, ex);
                }

                break;

            case ServiceConnectStopPublishCommand:
            case ServiceConnectStopConsumeCommand:
            case ServiceConnectStopSendCommand:
                if (activity is null)
                {
                    ServiceConnectInstrumentationEventSource.Log.NullActivity(name);
                    return;
                }

                if (activity.Source != ServiceConnectSource)
                {
                    return;
                }

                activity.Stop();

                break;
        }
    }
}
