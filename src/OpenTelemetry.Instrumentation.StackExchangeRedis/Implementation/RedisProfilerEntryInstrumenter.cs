// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using OpenTelemetry.Trace;
using StackExchange.Redis.Profiling;

#if NET
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#endif
namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Implementation;

internal static class RedisProfilerEntryInstrumenter
{
    private const int DefaultPort = 6379;

    private static readonly Lazy<Func<object, (string?, string?)>> MessageDataGetter = new(() =>
    {
        Type profiledCommandType = Type.GetType("StackExchange.Redis.Profiling.ProfiledCommand, StackExchange.Redis", throwOnError: true)!;
        Type scriptMessageType = Type.GetType("StackExchange.Redis.RedisDatabase+ScriptEvalMessage, StackExchange.Redis", throwOnError: true)!;

        var messageDelegate = CreateFieldGetter<object>(profiledCommandType, "Message", BindingFlags.NonPublic | BindingFlags.Instance);
        var scriptDelegate = CreateFieldGetter<string>(scriptMessageType, "script", BindingFlags.NonPublic | BindingFlags.Instance);
        var commandAndKeyFetcher = new PropertyFetcher<string>("CommandAndKey");

        if (messageDelegate == null)
        {
            return new Func<object, (string?, string?)>(source => (null, null));
        }

        return new Func<object, (string?, string?)>(source =>
        {
            if (source == null)
            {
                return (null, null);
            }

            var message = messageDelegate(source);
            if (message == null)
            {
                return (null, null);
            }

            string? script = null;
            if (message.GetType() == scriptMessageType)
            {
                script = scriptDelegate?.Invoke(message);
            }

            if (GetCommandAndKey(commandAndKeyFetcher, message, out var value))
            {
                return (value, script);
            }

            return (null, script);

#if NET
            [DynamicDependency("CommandAndKey", "StackExchange.Redis.Message", "StackExchange.Redis")]
            [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The CommandAndKey property is preserved by the above DynamicDependency")]
#endif
            static bool GetCommandAndKey(
                PropertyFetcher<string> commandAndKeyFetcher,
                object message,
#if NET
                [NotNullWhen(true)]
#endif
                out string? value)
            {
                return commandAndKeyFetcher.TryFetch(message, out value);
            }
        });
    });

    public static Activity? ProfilerCommandInstrument(
        Activity? parentActivity,
        IProfiledCommand command,
        RedisMetrics metrics,
        StackExchangeRedisInstrumentationOptions options)
    {
        var name = command.Command; // Example: SET;
        if (string.IsNullOrEmpty(name))
        {
            name = StackExchangeRedisConnectionInstrumentation.ActivityName;
        }

        var activity = StackExchangeRedisConnectionInstrumentation.ActivitySource.StartActivity(
            name,
            ActivityKind.Client,
            parentActivity?.Context ?? default,
            StackExchangeRedisConnectionInstrumentation.CreationTags,
            startTime: command.CommandCreated);

        if (activity is null && metrics.Enabled is false)
        {
            return null;
        }

        activity?.SetEndTime(command.CommandCreated + command.ElapsedTime);
        var meterTags = metrics.Enabled ?
            new TagList(StackExchangeRedisConnectionInstrumentation.CreationTags) :
            default(IList<KeyValuePair<string, object?>>);

        // see https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/database.md

        // Timing example:
        // command.CommandCreated; //2019-01-10 22:18:28Z

        // command.CreationToEnqueued;      // 00:00:32.4571995
        // command.EnqueuedToSending;       // 00:00:00.0352838
        // command.SentToResponse;          // 00:00:00.0060586
        // command.ResponseToCompletion;    // 00:00:00.0002601

        // Total:
        // command.ElapsedTime;             // 00:00:32.4988020

        activity?.SetTag(SemanticConventions.AttributeDbRedisFlagsKeyName, command.Flags.ToString());

        if (command.Command != null)
        {
            meterTags?.Add(SemanticConventions.AttributeDbOperationName, command.Command);
        }

        if (activity != null)
        {
            if (options.SetVerboseDatabaseStatements)
            {
                var (commandAndKey, script) = MessageDataGetter.Value.Invoke(command);

                if (!string.IsNullOrEmpty(commandAndKey) && !string.IsNullOrEmpty(script))
                {
                    activity.SetTag(SemanticConventions.AttributeDbStatement, commandAndKey + " " + script);
                }
                else if (!string.IsNullOrEmpty(commandAndKey))
                {
                    activity.SetTag(SemanticConventions.AttributeDbStatement, commandAndKey);
                }
                else if (command.Command != null)
                {
                    // Example: "db.statement": SET;
                    activity.SetTag(SemanticConventions.AttributeDbStatement, command.Command);
                }
            }
            else if (command.Command != null)
            {
                // Example: "db.statement": SET;
                activity.SetTag(SemanticConventions.AttributeDbStatement, command.Command);
            }
        }

        if (command.EndPoint != null)
        {
            if (command.EndPoint is IPEndPoint ipEndPoint)
            {
                var ip = ipEndPoint.Address.ToString();
                var port = ipEndPoint.Port;

                activity?.SetTag(SemanticConventions.AttributeNetPeerIp, ip);
                activity?.SetTag(SemanticConventions.AttributeNetPeerPort, port);

                meterTags?.Add(SemanticConventions.AttributeServerAddress, ip);
                meterTags?.Add(SemanticConventions.AttributeNetworkPeerAddress, ip);
                if (port != DefaultPort)
                {
                    meterTags?.Add(SemanticConventions.AttributeServerPort, port);
                    meterTags?.Add(SemanticConventions.AttributeNetworkPeerPort, port);
                }
            }
            else if (command.EndPoint is DnsEndPoint dnsEndPoint)
            {
                var host = dnsEndPoint.Host;
                var port = dnsEndPoint.Port;

                activity?.SetTag(SemanticConventions.AttributeNetPeerName, host);
                activity?.SetTag(SemanticConventions.AttributeNetPeerPort, port);

                meterTags?.Add(SemanticConventions.AttributeServerAddress, host);
                if (port != DefaultPort)
                {
                    meterTags?.Add(SemanticConventions.AttributeServerPort, port);
                }
            }
            else
            {
                var service = command.EndPoint.ToString();

                activity?.SetTag(SemanticConventions.AttributePeerService, service);
                meterTags?.Add(SemanticConventions.AttributeServerAddress, service);
            }
        }

        var db = command.Db;
        activity?.SetTag(SemanticConventions.AttributeDbRedisDatabaseIndex, db);
        meterTags?.Add(SemanticConventions.AttributeDbNamespace, db);

        // TODO: deal with the re-transmission
        // command.RetransmissionOf;
        // command.RetransmissionReason;

        if (activity?.IsAllDataRequested ?? false)
        {
            var enqueued = command.CommandCreated.Add(command.CreationToEnqueued);
            var send = enqueued.Add(command.EnqueuedToSending);
            var response = send.Add(command.SentToResponse);

            if (options.EnrichActivityWithTimingEvents)
            {
                activity.AddEvent(new ActivityEvent("Enqueued", enqueued));
                activity.AddEvent(new ActivityEvent("Sent", send));
                activity.AddEvent(new ActivityEvent("ResponseReceived", response));
            }

            options.Enrich?.Invoke(activity, command);
        }

        if (metrics.Enabled && meterTags is TagList meterTagList)
        {
            metrics.DurationHistogram.Record(command.ElapsedTime.TotalSeconds, meterTagList);
        }

        activity?.Stop();

        return activity;
    }

    public static void DrainSession(
        Activity? parentActivity,
        IEnumerable<IProfiledCommand> sessionCommands,
        RedisMetrics redisMetrics,
        StackExchangeRedisInstrumentationOptions options)
    {
        foreach (var command in sessionCommands)
        {
            ProfilerCommandInstrument(parentActivity, command, redisMetrics, options);
        }
    }

    /// <summary>
    /// Creates getter for a field defined in private or internal type
    /// represented with classType variable.
    /// </summary>
    private static Func<object, TField?>? CreateFieldGetter<TField>(
#if NET
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
#endif
        Type classType,
        string fieldName,
        BindingFlags flags)
    {
        FieldInfo? field = classType.GetField(fieldName, flags);
        if (field != null)
        {
#if NET
            if (RuntimeFeature.IsDynamicCodeSupported)
#endif
            {
                string methodName = classType.FullName + ".get_" + field.Name;
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
                // TODO: Remove the above disable when the AOT analyzer being used has the fix for https://github.com/dotnet/linker/issues/2715.
                DynamicMethod getterMethod = new DynamicMethod(methodName, typeof(TField), new[] { typeof(object) }, true);
#pragma warning restore IL3050
                ILGenerator generator = getterMethod.GetILGenerator();
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Castclass, classType);
                generator.Emit(OpCodes.Ldfld, field);
                generator.Emit(OpCodes.Ret);

                return (Func<object, TField>)getterMethod.CreateDelegate(typeof(Func<object, TField>));
            }
#if NET
            else
            {
                return obj => (TField?)field.GetValue(obj);
            }
#endif
        }

        return null;
    }

    private static void Add(this IList<KeyValuePair<string, object?>> tags, string ket, object? value)
    {
        tags?.Add(new KeyValuePair<string, object?>(ket, value));
    }
}
