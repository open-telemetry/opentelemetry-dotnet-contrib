// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.SshNet.Implementation;
using OpenTelemetry.Internal;
using Renci.SshNet;

namespace OpenTelemetry.Instrumentation.SshNet;

/// <summary>
/// Extension methods for wrapping SSH.NET clients with OpenTelemetry instrumentation.
/// </summary>
public static class SshNetClientExtensions
{
    /// <summary>
    /// Wraps an <see cref="ISftpClient"/> so its operations emit spans and metrics.
    /// </summary>
    /// <param name="client">The client to instrument, e.g. an <see cref="SftpClient"/>.</param>
    /// <returns>An instrumented <see cref="ISftpClient"/> that delegates to <paramref name="client"/>.
    /// Disposing it disposes <paramref name="client"/>.</returns>
    public static ISftpClient WithOpenTelemetry(this ISftpClient client) =>
        WithOpenTelemetry(client, configure: null);

    /// <summary>
    /// Wraps an <see cref="ISftpClient"/> so its operations emit spans and metrics.
    /// </summary>
    /// <param name="client">The client to instrument, e.g. an <see cref="SftpClient"/>.</param>
    /// <param name="configure">Callback action for configuring <see cref="SshNetInstrumentationOptions"/>.</param>
    /// <returns>An instrumented <see cref="ISftpClient"/> that delegates to <paramref name="client"/>.
    /// Disposing it disposes <paramref name="client"/>.</returns>
    public static ISftpClient WithOpenTelemetry(this ISftpClient client, Action<SshNetInstrumentationOptions>? configure)
    {
        Guard.ThrowIfNull(client);

        return client is InstrumentedSftpClient ? client : new InstrumentedSftpClient(client, CreateOptions(configure));
    }

    /// <summary>
    /// Wraps an <see cref="ISshClient"/> so its operations emit spans and metrics.
    /// </summary>
    /// <param name="client">The client to instrument, e.g. an <see cref="SshClient"/>.</param>
    /// <returns>An instrumented <see cref="ISshClient"/> that delegates to <paramref name="client"/>.
    /// Disposing it disposes <paramref name="client"/>.</returns>
    public static ISshClient WithOpenTelemetry(this ISshClient client) =>
        WithOpenTelemetry(client, configure: null);

    /// <summary>
    /// Wraps an <see cref="ISshClient"/> so its operations emit spans and metrics.
    /// </summary>
    /// <param name="client">The client to instrument, e.g. an <see cref="SshClient"/>.</param>
    /// <param name="configure">Callback action for configuring <see cref="SshNetInstrumentationOptions"/>.</param>
    /// <returns>An instrumented <see cref="ISshClient"/> that delegates to <paramref name="client"/>.
    /// Disposing it disposes <paramref name="client"/>.</returns>
    public static ISshClient WithOpenTelemetry(this ISshClient client, Action<SshNetInstrumentationOptions>? configure)
    {
        Guard.ThrowIfNull(client);

        return client is InstrumentedSshClient ? client : new InstrumentedSshClient(client, CreateOptions(configure));
    }

    private static SshNetInstrumentationOptions CreateOptions(Action<SshNetInstrumentationOptions>? configure)
    {
        var options = new SshNetInstrumentationOptions();
        configure?.Invoke(options);
        return options;
    }
}
