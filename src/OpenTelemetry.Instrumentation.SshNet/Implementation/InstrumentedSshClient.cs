// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace OpenTelemetry.Instrumentation.SshNet.Implementation;

/// <summary>
/// An <see cref="ISshClient"/> decorator that emits a span and a duration
/// measurement for <c>Connect</c> and <see cref="RunCommand"/>.
/// </summary>
/// <remarks>
/// <see cref="SshCommand"/> is sealed and has no hooks, so commands created
/// with <see cref="CreateCommand(string)"/> and executed later are not traced.
/// Shells and port forwarding are passed through.
/// </remarks>
/// <param name="inner">The client to decorate.</param>
/// <param name="options">The instrumentation options.</param>
internal sealed class InstrumentedSshClient(ISshClient inner, SshNetInstrumentationOptions options)
    : InstrumentedBaseClient<ISshClient>(inner, options, SshNetTelemetry.ProtocolSsh), ISshClient
{
    public IEnumerable<ForwardedPort> ForwardedPorts => this.Inner.ForwardedPorts;

    public SshCommand RunCommand(string commandText)
    {
        using var scope = this.StartScope("RunCommand");
        if (this.Options.RecordCommandText)
        {
            scope.SetTag(SshNetTelemetry.AttributeCommandText, commandText);
        }

        SshCommand command;
        try
        {
            command = this.Inner.RunCommand(commandText);
        }
        catch (Exception ex)
        {
            scope.SetException(ex);
            throw;
        }

        if (command.ExitStatus is { } exitStatus)
        {
            scope.SetTag(SshNetTelemetry.AttributeCommandExitCode, exitStatus);
        }

        return command;
    }

    public SshCommand CreateCommand(string commandText) =>
        this.Inner.CreateCommand(commandText);

    public SshCommand CreateCommand(string commandText, Encoding encoding) =>
        this.Inner.CreateCommand(commandText, encoding);

    public void AddForwardedPort(ForwardedPort port) =>
        this.Inner.AddForwardedPort(port);

    public void RemoveForwardedPort(ForwardedPort port) =>
        this.Inner.RemoveForwardedPort(port);

    public Shell CreateShell(Stream input, Stream output, Stream extendedOutput, string terminalName, uint columns, uint rows, uint width, uint height, IDictionary<TerminalModes, uint>? terminalModes, int bufferSize) =>
        this.Inner.CreateShell(input, output, extendedOutput, terminalName, columns, rows, width, height, terminalModes, bufferSize);

    public Shell CreateShell(Stream input, Stream output, Stream extendedOutput, string terminalName, uint columns, uint rows, uint width, uint height, IDictionary<TerminalModes, uint> terminalModes) =>
        this.Inner.CreateShell(input, output, extendedOutput, terminalName, columns, rows, width, height, terminalModes);

    public Shell CreateShell(Stream input, Stream output, Stream extendedOutput) =>
        this.Inner.CreateShell(input, output, extendedOutput);

    public Shell CreateShell(Encoding encoding, string input, Stream output, Stream extendedOutput, string terminalName, uint columns, uint rows, uint width, uint height, IDictionary<TerminalModes, uint>? terminalModes, int bufferSize) =>
        this.Inner.CreateShell(encoding, input, output, extendedOutput, terminalName, columns, rows, width, height, terminalModes, bufferSize);

    public Shell CreateShell(Encoding encoding, string input, Stream output, Stream extendedOutput, string terminalName, uint columns, uint rows, uint width, uint height, IDictionary<TerminalModes, uint> terminalModes) =>
        this.Inner.CreateShell(encoding, input, output, extendedOutput, terminalName, columns, rows, width, height, terminalModes);

    public Shell CreateShell(Encoding encoding, string input, Stream output, Stream extendedOutput) =>
        this.Inner.CreateShell(encoding, input, output, extendedOutput);

    public Shell CreateShellNoTerminal(Stream input, Stream output, Stream extendedOutput, int bufferSize = -1) =>
        this.Inner.CreateShellNoTerminal(input, output, extendedOutput, bufferSize);

    public ShellStream CreateShellStream(string terminalName, uint columns, uint rows, uint width, uint height, int bufferSize) =>
        this.Inner.CreateShellStream(terminalName, columns, rows, width, height, bufferSize);

    public ShellStream CreateShellStream(string terminalName, uint columns, uint rows, uint width, uint height, int bufferSize, IDictionary<TerminalModes, uint>? terminalModeValues) =>
        this.Inner.CreateShellStream(terminalName, columns, rows, width, height, bufferSize, terminalModeValues);

    public ShellStream CreateShellStreamNoTerminal(int bufferSize = -1) =>
        this.Inner.CreateShellStreamNoTerminal(bufferSize);
}
