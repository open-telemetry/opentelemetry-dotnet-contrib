// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Reflection;

namespace OpenTelemetry.Tests;

// Adapted from https://github.com/dotnet/aspnetcore/blob/3a973a5f4d28242262f27c86eb3f14299fe712ba/src/Testing/test/EventSourceValidatorTests.cs

internal static class EventSourceTestHelper
{
    /// <summary>
    /// Validates all <c>[Event]</c>-attributed methods on <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">A type that derives from <see cref="EventSource"/>.</typeparam>
    public static void ValidateEventSourceIds<T>()
        where T : EventSource
        => ValidateEventSourceIds(typeof(T));

    /// <summary>
    /// Validates all <c>[Event]</c>-attributed methods on the given <see cref="EventSource"/>-derived type.
    /// <para>
    /// Uses <see cref="EventSource.GenerateManifest(Type, string, EventManifestOptions)"/> with
    /// <see cref="EventManifestOptions.Strict"/> to perform IL-level validation that the integer
    /// argument passed to each <c>WriteEvent</c> call matches the <c>[Event(id)]</c> attribute
    /// on the calling method. This is the same validation the .NET runtime itself uses.
    /// </para>
    /// <para>
    /// Additionally checks for duplicate <see cref="EventAttribute.EventId"/> values across methods.
    /// </para>
    /// </summary>
    /// <param name="eventSourceType">A type that derives from <see cref="EventSource"/>.</param>
    public static void ValidateEventSourceIds(Type eventSourceType)
    {
        Assert.NotNull(eventSourceType);

        Assert.True(
            typeof(EventSource).IsAssignableFrom(eventSourceType),
            $"Type '{eventSourceType.FullName}' does not derive from EventSource.");

        var errors = new List<string>();

        // Check for duplicate Event IDs across methods.
        var seenIds = new Dictionary<int, string>();
        var methods = eventSourceType.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            var eventAttr = method.GetCustomAttribute<EventAttribute>();
            if (eventAttr is null)
            {
                continue;
            }

            if (seenIds.TryGetValue(eventAttr.EventId, out var existingMethod))
            {
                errors.Add(
                    $"Duplicate EventId {eventAttr.EventId}: methods '{existingMethod}' and '{method.Name}' share the same ID.");
            }
            else
            {
                seenIds[eventAttr.EventId] = method.Name;
            }
        }

        // Use GenerateManifest with Strict mode to validate that each method's
        // WriteEvent(id, ...) call uses an ID that matches its [Event(id)] attribute.
        // Internally this uses GetHelperCallFirstArg to IL-inspect the method body
        // and extract the integer constant passed to WriteEvent - the same validation
        // the .NET runtime performs when constructing an EventSource.
        try
        {
            var manifest = EventSource.GenerateManifest(
                eventSourceType,
                assemblyPathToIncludeInManifest: "assemblyPathForValidation",
                flags: EventManifestOptions.Strict);

            if (manifest is null)
            {
                errors.Add("GenerateManifest returned null, indicating the type is not a valid EventSource.");
            }
        }
        catch (ArgumentException ex)
        {
            errors.Add(ex.Message);
        }

        if (errors.Count > 0)
        {
            Assert.Fail(
                $"EventSource '{eventSourceType.FullName}' has event ID validation error(s):" +
                Environment.NewLine + string.Join(Environment.NewLine, errors));
        }
    }

    /// <summary>
    /// Invokes every method declared on the <see cref="EventSource"/> exposed by the static
    /// <c>Log</c> member of <typeparamref name="T"/> and verifies that each <c>[Event]</c> method
    /// writes an event with the expected ID and payload.
    /// </summary>
    /// <typeparam name="T">A type that derives from <see cref="EventSource"/> and has a static <c>Log</c> member.</typeparam>
    public static void ExerciseEventSource<T>()
        where T : EventSource
    {
        const BindingFlags StaticMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        var logField = typeof(T).GetField("Log", StaticMembers);
        var logProperty = typeof(T).GetProperty("Log", StaticMembers);

        var instance = (logField?.GetValue(null) ?? logProperty?.GetValue(null)) as T;
        Assert.True(instance is not null, $"Type '{typeof(T).FullName}' does not expose a static 'Log' member.");

        ExerciseEventSource(instance);
    }

    /// <summary>
    /// Invokes every method declared on the given <see cref="EventSource"/> with synthetic arguments
    /// and verifies that each <c>[Event]</c> method writes an event with the expected ID and payload.
    /// </summary>
    /// <param name="eventSource">The event source to exercise.</param>
    public static void ExerciseEventSource(EventSource eventSource)
    {
        Assert.NotNull(eventSource);

        using var listener = new CapturingEventListener(eventSource);

        var methods = eventSource.GetType().GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        var invoked = 0;

        foreach (var method in methods)
        {
            // Skip property accessors, generic methods and overrides of EventSource members.
            if (method.IsSpecialName ||
                method.IsGenericMethodDefinition ||
                method.GetBaseDefinition().DeclaringType != method.DeclaringType)
            {
                continue;
            }

            var parameters = method.GetParameters();
            var arguments = new object?[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                arguments[i] = CreateArgument(parameters[i].ParameterType);
            }

            var eventAttribute = method.GetCustomAttribute<EventAttribute>();
            listener.Events.Clear();

            try
            {
                method.Invoke(eventSource, arguments);
            }
            catch (TargetInvocationException ex)
            {
                Assert.Fail($"Invoking '{method.Name}' on '{eventSource.GetType().FullName}' threw: {ex.InnerException}");
            }

            invoked++;

            if (eventAttribute is null)
            {
                continue;
            }

            var written = listener.Events.SingleOrDefault(e => e.EventId == eventAttribute.EventId);

            Assert.True(
                written is not null,
                $"Invoking '{method.Name}' on '{eventSource.GetType().FullName}' did not write event {eventAttribute.EventId}.");

            Assert.Equal(parameters.Length, written.Payload?.Count ?? 0);

            if (eventAttribute.Message is not null)
            {
                // Accessing Message formats the payload, which verifies the message template.
                Assert.False(string.IsNullOrEmpty(written.Message), $"Event {eventAttribute.EventId} has an empty message.");
            }
        }

        Assert.True(invoked > 0, $"No methods were found on '{eventSource.GetType().FullName}'.");
    }

    private static object? CreateArgument(Type type)
    {
        if (type == typeof(string))
        {
            return "test";
        }

        if (type == typeof(Exception))
        {
            return new InvalidOperationException("test");
        }

        if (type == typeof(Activity))
        {
            return new Activity("test");
        }

        if (type.IsArray)
        {
            return Array.CreateInstance(type.GetElementType()!, 0);
        }

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private sealed class CapturingEventListener : EventListener
    {
        private readonly EventSource eventSource;
        private readonly int threadId = Environment.CurrentManagedThreadId;

        public CapturingEventListener(EventSource eventSource)
        {
            this.eventSource = eventSource;
            this.EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
        }

        public List<EventWrittenEventArgs> Events { get; } = [];

        public override void Dispose()
        {
            this.DisableEvents(this.eventSource);
            base.Dispose();
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventSource == this.eventSource && Environment.CurrentManagedThreadId == this.threadId)
            {
                this.Events.Add(eventData);
            }
        }
    }

    /// <summary>
    /// Event listener for testing event sources.
    /// </summary>
    /// <remarks>
    /// Adapted from https://github.com/microsoft/ApplicationInsights-aspnetcore/blob/bfccb57e4f632fa2ee627dacedeaa55644c739f5/NETCORE/test/Microsoft.ApplicationInsights.AspNetCore.Tests/Extensibility/Implementation/Tracing/TestEventListener.cs.
    /// </remarks>
#pragma warning disable CA1812
    private class TestEventListener : EventListener
#pragma warning restore CA1812
    {
        private readonly Queue<EventWrittenEventArgs> events;
        private readonly AutoResetEvent eventWritten;

        public TestEventListener()
        {
            this.events = new Queue<EventWrittenEventArgs>();
            this.eventWritten = new AutoResetEvent(false);
            this.OnOnEventWritten = e =>
            {
                this.events.Enqueue(e);
                this.eventWritten.Set();
            };
        }

        public Action<EventSource>? OnOnEventSourceCreated { get; set; }

        public Action<EventWrittenEventArgs> OnOnEventWritten { get; set; }

        public IEnumerable<EventWrittenEventArgs> Messages
        {
            get
            {
                if (this.events.Count == 0)
                {
                    this.eventWritten.WaitOne(TimeSpan.FromSeconds(5));
                }

                while (this.events.Count != 0)
                {
                    yield return this.events.Dequeue();
                }
            }
        }

        public void ClearMessages() =>
            this.events.Clear();

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
            => this.OnOnEventWritten(eventData);

        protected override void OnEventSourceCreated(EventSource eventSource) =>
            this.OnOnEventSourceCreated?.Invoke(eventSource);
    }
}
