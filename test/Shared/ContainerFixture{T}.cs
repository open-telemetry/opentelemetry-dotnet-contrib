// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using DotNet.Testcontainers.Containers;

namespace OpenTelemetry.Tests;

public abstract class ContainerFixture<T> : ContainerFixture
    where T : IContainer
{
    public T TypedContainer => field ??= this.CreateContainer();

    protected override IContainer Container => this.TypedContainer;

    public virtual Task InitializeAsync() => this.StartAsync();

    protected abstract T CreateContainer();
}
