// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Internal.Listeners;

/// <summary>
/// Marker interface for OpAmp listeners.
/// </summary>
public interface IOpAmpListener
{
}

/// <summary>
/// A listener capable of handling OpAmp messages of a specific type.
/// </summary>
/// <typeparam name="TMessage">The <see cref="IOpAmpMessage"/> type this listener handles.</typeparam>
public interface IOpAmpListener<TMessage> : IOpAmpListener
    where TMessage : IOpAmpMessage
{
    /// <summary>
    /// Handles the specified OpAmp message.
    /// </summary>
    /// <param name="message">The OpAmp message to handle.</param>
    void HandleMessage(TMessage message);
}
