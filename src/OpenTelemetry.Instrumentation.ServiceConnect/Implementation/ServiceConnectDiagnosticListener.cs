using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry.Trace;
using ServiceConnect.Interfaces;

namespace OpenTelemetry.Instrumentation.ServiceConnect.Implementation;

internal sealed class ServiceConnectDiagnosticListener : ListenerHandler
{
    public const string DiagnosticSourceName = "ServiceConnect.Bus";

    public const string ServiceConnectStartPublishCommand = "ServiceConnect.Bus.StartPublish";
    public const string ServiceConnectStopPublishCommand = "ServiceConnect.Bus.StopPublish";

    internal static readonly string ActivitySourceName = typeof(ServiceConnectDiagnosticListener).Assembly.GetName().Name ?? "ServiceConnect";
    internal static readonly string ActivityName = ActivitySourceName + ".Bus";

    internal static readonly Version? Version = typeof(ServiceConnectDiagnosticListener).Assembly.GetName().Version;
    internal static readonly ActivitySource ServiceConnectSource = new(ActivitySourceName, Version?.ToString() ?? "0.0.0");

    private readonly PropertyFetcher<string> routingKeyFetcher = new("RoutingKey");
    private readonly PropertyFetcher<Dictionary<string, string>?> stringHeadersFetcher = new("Headers");
    private readonly PropertyFetcher<Message> genericMessageFetcher = new("Message");

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
                    ServiceConnectPublishActivitySource.ActivityName,
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

                _ = this.genericMessageFetcher.TryFetch(payload, out Message? message);
                if (message is not null)
                {
                    _ = activity.SetTag(SemanticConventions.AttributeMessagingConversationId, message.CorrelationId.ToString());
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

            case ServiceConnectStopPublishCommand:
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
