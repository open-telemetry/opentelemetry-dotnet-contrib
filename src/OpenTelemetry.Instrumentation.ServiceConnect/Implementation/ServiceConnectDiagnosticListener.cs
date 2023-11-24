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

    internal static readonly string ActivitySourceName = typeof(ServiceConnectDiagnosticListener).Assembly.GetName().Name ?? "ServiceConnect";
    internal static readonly string ActivityName = ActivitySourceName + ".Bus";

    internal static readonly Version? Version = typeof(ServiceConnectDiagnosticListener).Assembly.GetName().Version;
    internal static readonly ActivitySource ServiceConnectSource = new(ActivitySourceName, Version?.ToString() ?? "0.0.0");

    private readonly PropertyFetcher<string> routingKeyFetcher = new("RoutingKey");
    private readonly PropertyFetcher<Dictionary<string, string>?> stringHeadersFetcher = new("Headers");
    private readonly PropertyFetcher<byte[]> messageFetcher = new("Message");
    private readonly PropertyFetcher<Message> genericMessageFetcher = new("Message");
    private readonly PropertyFetcher<IDictionary<string, object>> consumeHeadersFetcher = new("Headers");

    public ServiceConnectDiagnosticListener(string sourceName)
        : base(sourceName)
    {
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
                    return;
                }

                operation = "publish";
                _ = this.routingKeyFetcher.TryFetch(payload, out string publishRoutingKey);

                activity.DisplayName = (publishRoutingKey ?? "anyonymous") + " " + operation;

                _ = activity.SetTag(SemanticConventions.AttributeMessagingSystem, "rabbitmq");
                _ = activity.SetTag(SemanticConventions.AttributeMessagingProtocol, "amqp");
                _ = activity.SetTag(SemanticConventions.AttributeMessagingOperation, operation);

                _ = this.genericMessageFetcher.TryFetch(payload, out Message? publishMessage);
                if (publishMessage is not null)
                {
                    _ = activity.SetTag(SemanticConventions.AttributeMessagingConversationId, publishMessage.CorrelationId.ToString());
                }

                if (!string.IsNullOrEmpty(publishRoutingKey))
                {
                    _ = activity.SetTag(SemanticConventions.AttributeMessagingDestination, publishRoutingKey);
                }
                else
                {
                    _ = activity.SetTag("messaging.destination.anonymous", true);
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
                activity.DisplayName = (destinationAddress ?? "aynonymous") + " " + operation;

                _ = activity.SetTag(SemanticConventions.AttributeMessagingSystem, "rabbitmq");
                _ = activity.SetTag(SemanticConventions.AttributeMessagingProtocol, "amqp");
                _ = activity.SetTag(SemanticConventions.AttributeMessagingOperation, operation);
                _ = activity.SetTag(SemanticConventions.AttributeMessagingMessageId, readableHeaders["MessageId"]);
                _ = activity.SetTag(SemanticConventions.AttributeMessagingDestination, readableHeaders["DestinationAddress"]);

                byte[] consumeMessage = this.messageFetcher.Fetch(payload);
                _ = activity.SetTag(SemanticConventions.AttributeMessagingPayloadSize, consumeMessage.Length);

                break;

            case ServiceConnectStopPublishCommand:
            case ServiceConnectStopConsumeCommand:
                if (activity is null)
                {
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
