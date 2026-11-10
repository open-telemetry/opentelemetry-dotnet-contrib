// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Runtime;
using OpenTelemetry.Instrumentation.AWS.Implementation;
using SNS = Amazon.SimpleNotificationService.Model;
using SQS = Amazon.SQS.Model;

namespace OpenTelemetry.Instrumentation.AWS.Tests.Implementation;

/// <summary>
/// Verifies that <c>CanInjectContext</c>, which is used to decide the kind of a span before it is created,
/// agrees with whether <c>AddAttributes</c> injects the context when the request is later sent.
/// </summary>
public class CanInjectContextTests
{
    private const string Injected = "00-5759e988bd862e3fe1be46a994272793-53995c3f42cd8ad8-01";
    private const string Replacement = "00-5759e988bd862e3fe1be46a994272793-a1b2c3d4e5f60718-01";

    // isSqs, appAttributes, appKey, ignoreCase, earlierInjection, earlierInjectionChanged, expected
    public static TheoryData<bool, int, string?, bool, bool, bool, bool> Scenarios()
    {
        var data = new TheoryData<bool, int, string?, bool, bool, bool, bool>();

        foreach (var isSqs in new[] { true, false })
        {
            // Attributes supplied by the application that leave room for the injected ones
            data.Add(isSqs, -1, null, false, false, false, true); // no collection at all
            data.Add(isSqs, 0, null, false, false, false, true);
            data.Add(isSqs, 8, null, false, false, false, true); // 8 + 2 = 10
            data.Add(isSqs, 9, null, false, false, false, false); // 9 + 2 = 11

            // A name that is the same as one of the injected ones prevents injection
            data.Add(isSqs, 0, "traceparent", false, false, false, false);
            data.Add(isSqs, 0, "tracestate", false, false, false, false);

            // A name that differs only by case matters only if the collection ignores case
            data.Add(isSqs, 0, "TraceParent", false, false, false, true);
            data.Add(isSqs, 0, "TraceParent", true, false, false, false);

            // Values injected by an earlier send are replaced and not counted
            data.Add(isSqs, 0, null, false, true, false, true);
            data.Add(isSqs, 8, null, false, true, false, true);
            data.Add(isSqs, 9, null, false, true, false, false);

            // Unless the application changed them, when they are its own
            data.Add(isSqs, 0, null, false, true, true, false);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void CanInjectContext_MatchesWhetherAddAttributesInjects(
        bool isSqs,
        int appAttributes,
        string? appKey,
        bool ignoreCase,
        bool earlierInjection,
        bool earlierInjectionChanged,
        bool expected)
    {
        var scenario = new Scenario(appAttributes, appKey, ignoreCase, earlierInjection, earlierInjectionChanged);

        var (predicted, injected) = isSqs ? EvaluateSqs(scenario) : EvaluateSns(scenario);

        Assert.Equal(expected, predicted);
        Assert.Equal(expected, injected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanInjectContext_NothingToInject_ReturnsFalse(bool isSqs)
    {
        var attributes = new Dictionary<string, string>();

        Assert.False(isSqs
            ? SqsRequestContextHelper.CanInjectContext([], attributes)
            : SnsRequestContextHelper.CanInjectContext([], attributes));
    }

    private static (bool Predicted, bool Injected) EvaluateSqs(Scenario scenario)
        => Evaluate(
            scenario,
            attributes => new SQS.SendMessageRequest { MessageAttributes = attributes },
            comparer => new Dictionary<string, SQS.MessageAttributeValue>(comparer),
            value => new SQS.MessageAttributeValue { DataType = "String", StringValue = value },
            SqsRequestContextHelper.CanInjectContext,
            SqsRequestContextHelper.AddAttributes);

    private static (bool Predicted, bool Injected) EvaluateSns(Scenario scenario)
        => Evaluate(
            scenario,
            attributes => new SNS.PublishRequest { MessageAttributes = attributes },
            comparer => new Dictionary<string, SNS.MessageAttributeValue>(comparer),
            value => new SNS.MessageAttributeValue { DataType = "String", StringValue = value },
            SnsRequestContextHelper.CanInjectContext,
            SnsRequestContextHelper.AddAttributes);

    private static (bool Predicted, bool Injected) Evaluate<TRequest, TValue>(
        Scenario scenario,
        Func<Dictionary<string, TValue>?, TRequest> createRequest,
        Func<StringComparer, Dictionary<string, TValue>> createAttributes,
        Func<string, TValue> createValue,
        Func<Dictionary<string, TValue>?, IReadOnlyDictionary<string, string>, bool> canInjectContext,
        Action<IRequestContext, IReadOnlyDictionary<string, string>> addAttributes)
        where TRequest : AmazonWebServiceRequest
    {
        var comparer = scenario.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var existing = scenario.AppAttributes < 0 ? null : createAttributes(comparer);

        for (var i = 0; i < scenario.AppAttributes; i++)
        {
            existing!.Add($"application-{i}", createValue("value"));
        }

        if (scenario.AppKey is not null)
        {
            existing!.Add(scenario.AppKey, createValue("value"));
        }

        var request = createRequest(existing);

        var propagated = new Dictionary<string, string>
        {
            ["traceparent"] = Injected,
            ["tracestate"] = "key=value",
        };

        if (scenario.EarlierInjection)
        {
            var earlier = new Dictionary<string, string>
            {
                ["traceparent"] = Replacement,
                ["tracestate"] = "earlier=value",
            };

            // The attributes are injected by sending the request, as the instrumentation does for each send.
            addAttributes(new TestRequestContext(request, new TestRequest()), earlier);

            if (scenario.EarlierInjectionChanged)
            {
                // The application replaces a value that was injected, so it is no longer unchanged.
                GetAttributes(request)!["traceparent"] = createValue(Replacement);
            }
        }

        var predicted = canInjectContext(GetAttributes(request), propagated);

        addAttributes(new TestRequestContext(request, new TestRequest()), propagated);

        var attributes = GetAttributes(request);
        var injected = attributes is not null &&
            attributes.TryGetValue("traceparent", out var traceParent) &&
            string.Equals(GetStringValue(traceParent), Injected, StringComparison.Ordinal);

        return (predicted, injected);

        static Dictionary<string, TValue>? GetAttributes(TRequest request) => request switch
        {
            SQS.SendMessageRequest sqs => (object)sqs.MessageAttributes as Dictionary<string, TValue>,
            SNS.PublishRequest sns => (object)sns.MessageAttributes as Dictionary<string, TValue>,
            _ => throw new NotSupportedException(),
        };

        static string? GetStringValue(TValue value) => value switch
        {
            SQS.MessageAttributeValue sqs => sqs.StringValue,
            SNS.MessageAttributeValue sns => sns.StringValue,
            _ => throw new NotSupportedException(),
        };
    }

    private sealed class Scenario(int appAttributes, string? appKey, bool ignoreCase, bool earlierInjection, bool earlierInjectionChanged)
    {
        public int AppAttributes { get; } = appAttributes;

        public string? AppKey { get; } = appKey;

        public bool IgnoreCase { get; } = ignoreCase;

        public bool EarlierInjection { get; } = earlierInjection;

        public bool EarlierInjectionChanged { get; } = earlierInjectionChanged;
    }
}
