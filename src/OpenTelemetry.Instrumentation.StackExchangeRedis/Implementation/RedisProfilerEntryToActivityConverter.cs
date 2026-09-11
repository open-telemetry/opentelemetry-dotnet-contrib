// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Net;
#if NET
using System.Net.Sockets;
#endif
using System.Reflection;
using System.Reflection.Emit;
using OpenTelemetry.Trace;
using StackExchange.Redis.Profiling;
#if NET
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
#endif

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Implementation;

internal static class RedisProfilerEntryToActivityConverter
{
    private static readonly string[] CachedDatabaseNames =
    [
        "0", "1", "2", "3", "4", "5", "6", "7",
        "8", "9", "10", "11", "12", "13", "14", "15",
    ];

    private static readonly Lazy<Func<object, (string?, string?)>> MessageDataGetter = new(() =>
    {
#pragma warning disable IDE0370 // Suppression is unnecessary
        var profiledCommandType = Type.GetType("StackExchange.Redis.Profiling.ProfiledCommand, StackExchange.Redis", throwOnError: true)!;
        var scriptMessageType = Type.GetType("StackExchange.Redis.RedisDatabase+ScriptEvalMessage, StackExchange.Redis", throwOnError: true)!;
#pragma warning restore IDE0370 // Suppression is unnecessary

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

            return GetCommandAndKey(commandAndKeyFetcher, message, out var value) ? (value, script) : (null, script);

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

    public static Activity? ProfilerCommandToActivity(Activity? parentActivity, IProfiledCommand command, StackExchangeRedisInstrumentationOptions options)
    {
        try
        {
            if (options.Filter != null && !options.Filter(new(parentActivity, command)))
            {
                return null;
            }
        }
        catch
        {
            return null;
        }

        var name = command.Command; // Example: SET;
        if (string.IsNullOrEmpty(name))
        {
            name = StackExchangeRedisConnectionInstrumentation.ActivityName;
        }

        var activitySource =
            options.EmitNewAttributes && options.EmitOldAttributes ?
            StackExchangeRedisConnectionInstrumentation.ActivitySourceBoth :
            options.EmitNewAttributes ?
            StackExchangeRedisConnectionInstrumentation.ActivitySourceNew :
            StackExchangeRedisConnectionInstrumentation.ActivitySource;

        var creationTags =
            options.EmitOldAttributes && options.EmitNewAttributes ? StackExchangeRedisConnectionInstrumentation.BothCreationTags :
            options.EmitNewAttributes ? StackExchangeRedisConnectionInstrumentation.NewCreationTags :
            options.EmitOldAttributes ? StackExchangeRedisConnectionInstrumentation.OldCreationTags :
            [];

        var activity = activitySource.StartActivity(
            name,
            ActivityKind.Client,
            parentActivity?.Context ?? default,
            creationTags,
            startTime: command.CommandCreated);

        if (activity == null)
        {
            return null;
        }

        activity.SetEndTime(command.CommandCreated + command.ElapsedTime);

        if (activity.IsAllDataRequested)
        {
            // See https://github.com/open-telemetry/semantic-conventions/blob/v1.42.0/docs/db/database-spans.md

            // Timing example:

            // command.CommandCreated;          // 2019-01-10 22:18:28Z
            // command.CreationToEnqueued;      // 00:00:32.4571995
            // command.EnqueuedToSending;       // 00:00:00.0352838
            // command.SentToResponse;          // 00:00:00.0060586
            // command.ResponseToCompletion;    // 00:00:00.0002601

            // Total:
            // command.ElapsedTime;             // 00:00:32.4988020

            string? commandAndKey = null;
            string? script = null;
            string? verboseStatement = null;
            if (options.SetVerboseDatabaseStatements)
            {
                (commandAndKey, script) = MessageDataGetter.Value.Invoke(command);

                if (!string.IsNullOrEmpty(commandAndKey))
                {
                    verboseStatement = string.IsNullOrEmpty(script)
                        ? commandAndKey
                        : string.Concat(commandAndKey, " ", script);
                }
            }

            if (options.EmitOldAttributes)
            {
                activity.SetTag(StackExchangeRedisConnectionInstrumentation.RedisDatabaseIndexKeyName, command.Db);

                // Example: "db.statement": SET;
                var statement = verboseStatement ?? command.Command;

                if (statement != null)
                {
                    activity.SetTag(SemanticConventions.AttributeDbStatement, statement);
                }
            }

            if (options.EmitNewAttributes)
            {
                var queryText = verboseStatement ?? command.Command;
                var db = command.Db;
                var dbNamespace = (uint)db < (uint)CachedDatabaseNames.Length
                    ? CachedDatabaseNames[db]
                    : db.ToString(CultureInfo.InvariantCulture);
                activity.SetTag(SemanticConventions.AttributeDbOperationName, command.Command);
                activity.SetTag(SemanticConventions.AttributeDbNamespace, dbNamespace);
                activity.SetTag(SemanticConventions.AttributeDbQueryText, queryText);
            }

            if (command.EndPoint != null)
            {
                if (command.EndPoint is IPEndPoint ipEndPoint)
                {
                    var address = ipEndPoint.Address.ToString();
                    var port = PortTelemetryHelper.GetBoxedPort(ipEndPoint.Port, cacheValue: true);
                    activity.SetTag(SemanticConventions.AttributeServerAddress, address);
                    activity.SetTag(SemanticConventions.AttributeServerPort, port);
                    activity.SetTag(SemanticConventions.AttributeNetworkPeerAddress, address);
                    activity.SetTag(SemanticConventions.AttributeNetworkPeerPort, port);
                }
                else if (command.EndPoint is DnsEndPoint dnsEndPoint)
                {
                    activity.SetTag(SemanticConventions.AttributeServerAddress, dnsEndPoint.Host);
                    activity.SetTag(SemanticConventions.AttributeServerPort, PortTelemetryHelper.GetBoxedPort(dnsEndPoint.Port, cacheValue: true));
                }
#if NET
                else if (command.EndPoint is UnixDomainSocketEndPoint unixDomainSocketEndPoint)
                {
                    var address = unixDomainSocketEndPoint.ToString();
                    activity.SetTag(SemanticConventions.AttributeServerAddress, address);
                    activity.SetTag(SemanticConventions.AttributeNetworkPeerAddress, address);
                }
#endif
            }

            // TODO: deal with the re-transmission
            // command.RetransmissionOf;
            // command.RetransmissionReason;

            var enqueued = command.CommandCreated.Add(command.CreationToEnqueued);
            var send = enqueued.Add(command.EnqueuedToSending);
            var response = send.Add(command.SentToResponse);

            if (options.EnrichActivityWithTimingEvents)
            {
                activity.AddEvent(new ActivityEvent("Enqueued", enqueued));
                activity.AddEvent(new ActivityEvent("Sent", send));
                activity.AddEvent(new ActivityEvent("ResponseReceived", response));
            }

            try
            {
                options.Enrich?.Invoke(activity, new(parentActivity, command));
            }
            catch
            {
                // exceptions in Enrich callback should not prevent the activity from being stopped.
            }
        }

        activity.Stop();

        return activity;
    }

    public static void DrainSession(
        Activity? parentActivity,
        IEnumerable<IProfiledCommand> sessionCommands,
        Baggage baggage,
        StackExchangeRedisInstrumentationOptions options)
    {
        // Whoever issued the commands isn't the one draining them, so their baggage has
        // to be put back by hand. Skipped when empty, or every Activity below pays for it.
        var previousBaggage = Baggage.Current;
        var applyBaggage = baggage.Count != 0;
        if (applyBaggage)
        {
            Baggage.Current = baggage;
        }

        try
        {
            foreach (var command in sessionCommands)
            {
                ProfilerCommandToActivity(parentActivity, command, options);
            }
        }
        finally
        {
            if (applyBaggage)
            {
                Baggage.Current = previousBaggage;
            }
        }
    }

    /// <summary>
    /// Creates getter for a field defined in private or internal type
    /// represented with classType variable.
    /// </summary>
    private static Func<object, TField?>? CreateFieldGetter<TField>(
#if NET
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
#if NET8_0
        [UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode", Justification = "Guarded by RuntimeFeature.IsDynamicCodeSupported.")]
#endif
#endif
        Type classType,
        string fieldName,
        BindingFlags flags)
    {
        var field = classType.GetField(fieldName, flags);
        if (field != null)
        {
#if NET
            if (RuntimeFeature.IsDynamicCodeSupported)
#endif
            {
                var methodName = classType.FullName + ".get_" + field.Name;
#if NET8_0
#pragma warning disable IL3050 // Avoid calling members annotated with 'RequiresDynamicCodeAttribute' when publishing as Native AOT
#endif
                var getterMethod = new DynamicMethod(methodName, typeof(TField), [typeof(object)], true);
#if NET8_0
#pragma warning restore IL3050 // Avoid calling members annotated with 'RequiresDynamicCodeAttribute' when publishing as Native AOT
#endif
                var generator = getterMethod.GetILGenerator();
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
}
