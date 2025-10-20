// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using DotNet.Testcontainers.Containers;

namespace MJCZone.DapperMatic.Tests.ProviderFixtures;

public abstract class DatabaseFixtureBase<TContainer> : IDatabaseFixture, IAsyncLifetime
    where TContainer : DockerContainer, IDatabaseContainer
{
    public abstract TContainer Container { get; }

    public virtual string ConnectionString => Container.GetConnectionString();
    public virtual string ContainerId => $"{Container.Id}";

    public virtual Task InitializeAsync() => Container.StartAsync();

    public virtual Task DisposeAsync() => Container.DisposeAsync().AsTask();

    public virtual bool IgnoreSqlType(string sqlType) => false;
}
