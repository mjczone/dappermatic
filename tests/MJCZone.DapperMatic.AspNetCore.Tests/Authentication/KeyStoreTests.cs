// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using FluentAssertions;
using MJCZone.DapperMatic.AspNetCore.Authentication;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Authentication;

/// <summary>
/// Where keys and tickets are kept: in memory, in a file, in a database (each of the four).
/// </summary>
public class KeyStoreTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public KeyStoreTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<string> Stores =>
        [
            "memory",
            "file",
            TestcontainersAssemblyFixture.DatasourceId_Sqlite,
            TestcontainersAssemblyFixture.DatasourceId_PostgreSql,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            TestcontainersAssemblyFixture.DatasourceId_MySql,
        ];

    [Theory]
    [MemberData(nameof(Stores))]
    public async Task Keys_are_kept_listed_revoked_and_touched_Async(string store)
    {
        var keys = KeysOf(store);
        var now = DateTimeOffset.UtcNow;
        var limit = DmPolicy.Allow(DmAction.Read, "*").OnTables("orders");
        var owner = "o" + Guid.NewGuid().ToString("N")[..8];
        DapperMaticApiKey Key(string id, DapperMaticKeyKind kind, string? tenant, int minutes) =>
            new() { Id = id, SecretHash = new string('a', 64), Name = id, Kind = kind, Owner = owner, Tenant = tenant, CreatedAt = now.AddMinutes(minutes) };
        var first = Key(owner + "1", DapperMaticKeyKind.Service, "7", 0);
        first.Limits.Add(limit);
        await keys.SaveAsync(first);
        await keys.SaveAsync(Key(owner + "2", DapperMaticKeyKind.Personal, null, 1));

        var read = await keys.GetAsync(owner + "1");
        read!.Tenant.Should().Be("7");
        read.Limits.Should().ContainSingle().Which.Tables.Should().Equal("orders");
        (await keys.GetAsync("nobody")).Should().BeNull();

        (await keys.ListAsync(new DapperMaticKeyFilter { Owner = owner })).Select(k => k.Id).Should().Equal(owner + "2", owner + "1");
        (await keys.ListAsync(new DapperMaticKeyFilter { Owner = owner, Kind = DapperMaticKeyKind.Service })).Select(k => k.Id).Should().Equal(owner + "1");
        (await keys.ListAsync(new DapperMaticKeyFilter { Owner = owner, Tenant = string.Empty })).Select(k => k.Id).Should().Equal(owner + "2");

        (await keys.RevokeAsync(owner + "1", now)).Should().BeTrue();
        (await keys.RevokeAsync(owner + "1", now)).Should().BeFalse();
        (await keys.GetAsync(owner + "1"))!.IsLive(now.AddSeconds(1)).Should().BeFalse();

        await keys.TouchAsync(new Dictionary<string, DateTimeOffset> { [owner + "2"] = now });
        (await keys.GetAsync(owner + "2"))!.LastUsedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public async Task A_ticket_is_taken_once_before_it_expires_Async(string store)
    {
        if (store == "file")
        {
            return; // Tickets beside a file are kept in memory.
        }

        var tickets = TicketsOf(store);
        var id = Guid.NewGuid().ToString("N");
        await tickets.SaveAsync(id, "payload", DateTimeOffset.UtcNow.AddSeconds(30), CancellationToken.None);
        (await tickets.TakeAsync(id, CancellationToken.None)).Should().Be("payload");
        (await tickets.TakeAsync(id, CancellationToken.None)).Should().BeNull();

        var stale = Guid.NewGuid().ToString("N");
        await tickets.SaveAsync(stale, "payload", DateTimeOffset.UtcNow.AddSeconds(-1), CancellationToken.None);
        (await tickets.TakeAsync(stale, CancellationToken.None)).Should().BeNull();
    }

    private IDapperMaticKeyRepository KeysOf(string store) =>
        store switch
        {
            "memory" => new InMemoryDapperMaticKeyRepository(),
            "file" => new FileDapperMaticKeyRepository(Path.Combine(Path.GetTempPath(), $"dm-keys-{Guid.NewGuid():N}.json")),
            _ => Datasource(store) is var (provider, connection) ? new DatabaseDapperMaticKeyRepository(provider, connection, new DbConnectionFactory(Providers())) : null!,
        };

    private IDapperMaticTicketStore TicketsOf(string store) =>
        store == "memory"
            ? new InMemoryDapperMaticTicketStore()
            : Datasource(store) is var (provider, connection) ? new DatabaseDapperMaticTicketStore(provider, connection, new DbConnectionFactory(Providers()), null) : null!;

    private (string Provider, string ConnectionString) Datasource(string id)
    {
        var datasource = _fixture.GetTestDatasources().Single(d => d.Id == id);
        return (datasource.Provider!, datasource.ConnectionString!);
    }

    private static Microsoft.Extensions.Options.IOptions<DapperMaticOptions> Providers()
    {
        var options = new DapperMaticOptions();
        options.ProviderFactories[DbProviderType.Sqlite] = Microsoft.Data.Sqlite.SqliteFactory.Instance;
        options.ProviderFactories[DbProviderType.PostgreSql] = Npgsql.NpgsqlFactory.Instance;
        options.ProviderFactories[DbProviderType.SqlServer] = Microsoft.Data.SqlClient.SqlClientFactory.Instance;
        options.ProviderFactories[DbProviderType.MySql] = MySqlConnector.MySqlConnectorFactory.Instance;
        return Microsoft.Extensions.Options.Options.Create(options);
    }
}
