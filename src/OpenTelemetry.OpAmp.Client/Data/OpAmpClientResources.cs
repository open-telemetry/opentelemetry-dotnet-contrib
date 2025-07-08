// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.OpAmp.Client.Data;

/// <summary>
/// Represents a collection of resources.
/// </summary>
public class OpAmpClientResources
{
    /// <summary>
    /// Gets the collection of resources associated with the current instance.
    /// </summary>
    public Dictionary<string, AnyValueUnion> IdentifingResources { get; } = [];

    /// <summary>
    /// Gets the collection of resources associated with the current instance.
    /// </summary>
    public Dictionary<string, AnyValueUnion> NonIdentifingResources { get; } = [];

    /// <summary>
    /// Adds an itentifying attribute to the resources collection.
    /// </summary>
    /// <param name="key">The unique key associated with the resource.</param>
    /// <param name="value">The value of the resource to be added.</param>
    public void AddIdentifyingAttribute(string key, string value)
    {
        Guard.ThrowIfNullOrEmpty(key, nameof(key));

        this.IdentifingResources[key] = AnyValueUnion.From(value);
    }

    /// <summary>
    /// Adds an itentifying attribute to the resources collection.
    /// </summary>
    /// <param name="key">The unique key associated with the resource.</param>
    /// <param name="value">The value of the resource to be added.</param>
    public void AddIdentifyingAttribute(string key, int value)
    {
        Guard.ThrowIfNullOrEmpty(key, nameof(key));

        this.IdentifingResources[key] = AnyValueUnion.From(value);
    }

    /// <summary>
    /// Adds an itentifying attribute to the resources collection.
    /// </summary>
    /// <param name="key">The unique key associated with the resource.</param>
    /// <param name="value">The value of the resource to be added.</param>
    public void AddIdentifyingAttribute(string key, double value)
    {
        Guard.ThrowIfNullOrEmpty(key, nameof(key));

        this.IdentifingResources[key] = AnyValueUnion.From(value);
    }

    /// <summary>
    /// Adds an itentifying attribute to the resources collection.
    /// </summary>
    /// <param name="key">The unique key associated with the resource.</param>
    /// <param name="value">The value of the resource to be added.</param>
    public void AddIdentifyingAttribute(string key, bool value)
    {
        Guard.ThrowIfNullOrEmpty(key, nameof(key));

        this.IdentifingResources[key] = AnyValueUnion.From(value);
    }

    /// <summary>
    /// Adds a non itentifying attribute to the resources collection.
    /// </summary>
    /// <param name="key">The unique key associated with the resource.</param>
    /// <param name="value">The value of the resource to be added.</param>
    public void AddNonIdentifyingAttribute(string key, string value)
    {
        Guard.ThrowIfNullOrEmpty(key, nameof(key));

        this.NonIdentifingResources[key] = AnyValueUnion.From(value);
    }

    /// <summary>
    /// Adds a non itentifying attribute to the resources collection.
    /// </summary>
    /// <param name="key">The unique key associated with the resource.</param>
    /// <param name="value">The value of the resource to be added.</param>
    public void AddNonIdentifyingAttribute(string key, int value)
    {
        Guard.ThrowIfNullOrEmpty(key, nameof(key));

        this.NonIdentifingResources[key] = AnyValueUnion.From(value);
    }

    /// <summary>
    /// Adds a non itentifying attribute to the resources collection.
    /// </summary>
    /// <param name="key">The unique key associated with the resource.</param>
    /// <param name="value">The value of the resource to be added.</param>
    public void AddNonIdentifyingAttribute(string key, double value)
    {
        Guard.ThrowIfNullOrEmpty(key, nameof(key));

        this.NonIdentifingResources[key] = AnyValueUnion.From(value);
    }

    /// <summary>
    /// Adds a non itentifying attribute to the resources collection.
    /// </summary>
    /// <param name="key">The unique key associated with the resource.</param>
    /// <param name="value">The value of the resource to be added.</param>
    public void AddNonIdentifyingAttribute(string key, bool value)
    {
        Guard.ThrowIfNullOrEmpty(key, nameof(key));

        this.NonIdentifingResources[key] = AnyValueUnion.From(value);
    }
}
