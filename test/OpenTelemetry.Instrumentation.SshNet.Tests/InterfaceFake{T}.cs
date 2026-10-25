// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace OpenTelemetry.Instrumentation.SshNet.Tests;

/// <summary>
/// A minimal <see cref="DispatchProxy"/>-based fake of an interface: records
/// every call and returns default values unless a handler is registered for
/// the member name.
/// </summary>
/// <typeparam name="T">The interface to fake.</typeparam>
#pragma warning disable CA1515 // DispatchProxy requires the proxy type to be public
public class InterfaceFake<T> : DispatchProxy
#pragma warning restore CA1515
    where T : class
{
    private readonly Dictionary<string, Func<object?[], object?>> handlers = [];
    private readonly List<(string Name, object?[] Args)> calls = [];

    public T Instance { get; internal set; } = null!;

    /// <summary>
    /// Registers a handler for every overload of the named member. Property
    /// getters are named <c>get_{Property}</c>.
    /// </summary>
    /// <param name="memberName">The member name.</param>
    /// <param name="handler">Receives the call arguments and returns the result.</param>
    /// <returns>This fake, for chaining.</returns>
    public InterfaceFake<T> On(string memberName, Func<object?[], object?> handler)
    {
        this.handlers[memberName] = handler;
        return this;
    }

    public IEnumerable<object?[]> CallsTo(string memberName) =>
        this.calls.Where(c => c.Name == memberName).Select(c => c.Args);

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var name = targetMethod!.Name;
        args ??= [];
        this.calls.Add((name, args));

        return this.handlers.TryGetValue(name, out var handler)
            ? handler(args)
            : DefaultValue(targetMethod.ReturnType);
    }

    private static object? DefaultValue(Type type)
    {
        if (type == typeof(void))
        {
            return null;
        }

        if (type == typeof(Task))
        {
            return Task.CompletedTask;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = type.GetGenericArguments()[0];
            return typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, [DefaultValue(resultType)]);
        }

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
