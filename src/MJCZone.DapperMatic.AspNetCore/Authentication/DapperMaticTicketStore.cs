// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Data;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Dml;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.AspNetCore.Authentication;

/// <summary>
/// Keeps the tickets made and not yet used: in the database DapperMatic keeps access in (every instance redeems any
/// ticket, once), or in memory (one instance).
/// </summary>
internal interface IDapperMaticTicketStore
{
    /// <summary>
    /// Keeps a ticket.
    /// </summary>
    /// <param name="id">Its id: what the URL carries.</param>
    /// <param name="payload">What it signs in, protected.</param>
    /// <param name="expires">When it stops being good.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(string id, string payload, DateTimeOffset expires, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a ticket: its payload once, if it has not expired; removed whatever happens, so that it is never taken twice.
    /// </summary>
    /// <param name="id">Its id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Its payload, or null.</returns>
    Task<string?> TakeAsync(string id, CancellationToken cancellationToken);
}

/// <summary>Tickets kept in memory: redeemed only by the instance that made them.</summary>
internal sealed class InMemoryDapperMaticTicketStore : IDapperMaticTicketStore
{
    private readonly ConcurrentDictionary<string, (string Payload, DateTimeOffset Expires)> _tickets = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task SaveAsync(string id, string payload, DateTimeOffset expires, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (old, ticket) in _tickets)
        {
            if (ticket.Expires <= now)
            {
                _tickets.TryRemove(old, out _);
            }
        }

        _tickets[id] = (payload, expires);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> TakeAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(_tickets.TryRemove(id, out var ticket) && ticket.Expires > DateTimeOffset.UtcNow ? ticket.Payload : null);
}

/// <summary>
/// Tickets kept in a table beside DapperMatic's access, which every instance reads: a ticket is taken by deleting its
/// row, which one request alone can do.
/// </summary>
internal sealed class DatabaseDapperMaticTicketStore : IDapperMaticTicketStore
{
    private readonly string _provider;
    private readonly string _connectionString;
    private readonly IDbConnectionFactory _connections;
    private readonly string? _schemaName;
    private readonly string _tableName;
    private int _created;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseDapperMaticTicketStore"/> class.
    /// </summary>
    /// <param name="provider">The database provider.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="connections">The connection factory.</param>
    /// <param name="schemaName">The schema, or null for the default.</param>
    /// <param name="tableName">The table.</param>
    public DatabaseDapperMaticTicketStore(string provider, string connectionString, IDbConnectionFactory connections, string? schemaName, string tableName = "dm_tickets")
    {
        _provider = provider;
        _connectionString = connectionString;
        _connections = connections;
        _schemaName = schemaName;
        _tableName = tableName;
    }

    /// <inheritdoc />
    public async Task SaveAsync(string id, string payload, DateTimeOffset expires, CancellationToken cancellationToken)
    {
        using var db = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var scope = Scope(db);
        var now = DateTimeOffset.UtcNow;
        await scope.DeleteAsync<TicketRow>(t => t.ExpiresAt < now, cancellationToken: cancellationToken).ConfigureAwait(false);
        await scope.InsertAsync(new TicketRow { Id = id, Payload = payload, ExpiresAt = expires }, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string?> TakeAsync(string id, CancellationToken cancellationToken)
    {
        using var db = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var scope = Scope(db);
        var row = await scope.SingleByIdAsync<TicketRow>(id, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        // Whoever deletes the row has the ticket: a second request deletes nothing.
        var taken = await scope.DeleteAsync<TicketRow>(t => t.Id == id, cancellationToken: cancellationToken).ConfigureAwait(false) == 1;
        return taken && row.ExpiresAt > DateTimeOffset.UtcNow ? row.Payload : null;
    }

    private DmScope Scope(IDbConnection db)
    {
        var scope = new DmScope(db).WithTableName<TicketRow>(_tableName);
        return _schemaName is null ? scope : scope.InSchema(_schemaName);
    }

    private async Task<IDbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var db = _connections.CreateConnection(_provider, _connectionString);
        db.Open();
        if (Volatile.Read(ref _created) == 0)
        {
            await db.CreateTableIfNotExistsAsync(
                    new DmTable(
                        _schemaName,
                        _tableName,
                        [
                            new DmColumn("id", typeof(string), length: 64),
                            new DmColumn("payload", typeof(string), length: int.MaxValue),
                            new DmColumn("expires_at", typeof(DateTimeOffset)),
                        ],
                        primaryKey: new DmPrimaryKeyConstraint($"pk_{_tableName}", [new DmOrderedColumn("id")])
                    ),
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
            Volatile.Write(ref _created, 1);
        }

        return db;
    }

#pragma warning disable SA1600 // The row's members are the table's columns.
    [DmTable(null, "dm_tickets")]
    private sealed class TicketRow
    {
        [DmColumn("id", length: 64, isPrimaryKey: true)]
        public string Id { get; set; } = string.Empty;

        [DmColumn("payload", length: int.MaxValue)]
        public string Payload { get; set; } = string.Empty;

        [DmColumn("expires_at")]
        public DateTimeOffset ExpiresAt { get; set; }
    }
#pragma warning restore SA1600
}
