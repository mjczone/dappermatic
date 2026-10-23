// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Buffers.Binary;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dapper;

namespace MJCZone.DapperMatic.Migrations.Internal;

/// <summary>
/// The lock that makes instances migrating one database take turns, using each provider's own mechanism.
/// </summary>
/// <remarks>
/// <para>
/// Every mechanism here is released by the database when the connection goes away, so an instance that dies
/// mid-run cannot hold the lock forever. A lock row in a table would need a lease, a heartbeat and an expiry
/// instead, and a wrong expiry either stalls the deploy or lets two migrators run at once.
/// </para>
/// <para>
/// SQL Server and PostgreSQL take a lock owned by the run's transaction, so it ends with it. On PostgreSQL that
/// also keeps it correct under Npgsql multiplexing, where a session-level advisory lock could be taken on one
/// physical connection and released on another. MySQL has no transactional DDL, so the run has no transaction
/// and the lock is the session's, released explicitly. On SQLite the run's write transaction is itself the
/// lock: only one connection can hold one.
/// </para>
/// </remarks>
internal static class MigrationLock
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Derives the provider-facing lock name from a seed. A hash keeps it within MySQL's 64 characters whatever
    /// the database and ledger are called.
    /// </summary>
    /// <param name="seed">The text the name is derived from.</param>
    /// <returns>The lock name.</returns>
    public static string GetName(string seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("dm-migrations\n" + seed.ToLowerInvariant()));
        return "dm_migrations_" + Convert.ToHexStringLower(hash)[..48];
    }

    /// <summary>
    /// Takes the lock, waiting for another instance to release it for at most <paramref name="timeout"/>.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The run's transaction; required on SQL Server and PostgreSQL, null on MySQL.</param>
    /// <param name="name">The lock name, from <see cref="GetName"/>.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <param name="waiting">Called once if the lock is held by someone else, before waiting for it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A function that releases the lock, or null when it is released with the transaction.</returns>
    /// <exception cref="TimeoutException">The lock was not released in time.</exception>
    public static async Task<Func<Task>?> AcquireAsync(
        IDbConnection db,
        IDbTransaction? tx,
        string name,
        TimeSpan timeout,
        Action waiting,
        CancellationToken cancellationToken
    )
    {
        switch (db.GetDbProviderType())
        {
            case DbProviderType.SqlServer:
                if (!await TrySqlServerAsync(db, tx, name, TimeSpan.Zero, cancellationToken).ConfigureAwait(false))
                {
                    waiting();
                    if (!await TrySqlServerAsync(db, tx, name, timeout, cancellationToken).ConfigureAwait(false))
                    {
                        throw TimedOut(name, timeout);
                    }
                }

                return null;

            case DbProviderType.PostgreSql:
                var key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(name)));
                var deadline = DateTime.UtcNow + timeout;
                var first = true;
                while (
                    !await db.ExecuteScalarAsync<bool>(
                            new CommandDefinition(
                                "SELECT pg_try_advisory_xact_lock(@key)",
                                new { key },
                                tx,
                                cancellationToken: cancellationToken
                            )
                        )
                        .ConfigureAwait(false)
                )
                {
                    if (first)
                    {
                        waiting();
                        first = false;
                    }

                    if (DateTime.UtcNow >= deadline)
                    {
                        throw TimedOut(name, timeout);
                    }

                    await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
                }

                return null;

            case DbProviderType.MySql:
                if (!await TryMySqlAsync(db, name, 0, cancellationToken).ConfigureAwait(false))
                {
                    waiting();
                    var seconds = (int)Math.Ceiling(timeout.TotalSeconds);
                    if (!await TryMySqlAsync(db, name, seconds, cancellationToken).ConfigureAwait(false))
                    {
                        throw TimedOut(name, timeout);
                    }
                }

                return async () =>
                    await db.ExecuteScalarAsync<long?>(
                            new CommandDefinition(
                                "SELECT RELEASE_LOCK(@name)",
                                new { name },
                                cancellationToken: CancellationToken.None
                            )
                        )
                        .ConfigureAwait(false);

            case DbProviderType.Sqlite:
                // The run's transaction holds SQLite's single write lock already.
                return null;

            default:
                throw new NotSupportedException($"Migrations do not support {db.GetDbProviderType()}.");
        }
    }

    private static async Task<bool> TrySqlServerAsync(
        IDbConnection db,
        IDbTransaction? tx,
        string name,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        // 0 and 1 mean granted; -1 timed out; -2, -3 and -999 are a cancellation, a deadlock and an error.
        var result = await db.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    """
                    DECLARE @result int;
                    EXEC @result = sp_getapplock @Resource = @name, @LockMode = 'Exclusive',
                        @LockOwner = 'Transaction', @LockTimeout = @timeout;
                    SELECT @result;
                    """,
                    new { name, timeout = (int)timeout.TotalMilliseconds },
                    tx,
                    commandTimeout: (int)Math.Ceiling(timeout.TotalSeconds) + 30,
                    cancellationToken: cancellationToken
                )
            )
            .ConfigureAwait(false);

        return result switch
        {
            >= 0 => true,
            -1 => false,
            _ => throw new InvalidOperationException(
                $"SQL Server did not grant the migration lock '{name}' (sp_getapplock returned {result.ToString(CultureInfo.InvariantCulture)})."
            ),
        };
    }

    private static async Task<bool> TryMySqlAsync(
        IDbConnection db,
        string name,
        int seconds,
        CancellationToken cancellationToken
    )
    {
        // 1 means granted, 0 timed out, and NULL an error such as the thread being killed.
        var result = await db.ExecuteScalarAsync<long?>(
                new CommandDefinition(
                    "SELECT GET_LOCK(@name, @seconds)",
                    new { name, seconds },
                    commandTimeout: seconds + 30,
                    cancellationToken: cancellationToken
                )
            )
            .ConfigureAwait(false);

        return result switch
        {
            1 => true,
            0 => false,
            _ => throw new InvalidOperationException($"MySQL did not grant the migration lock '{name}'."),
        };
    }

    private static TimeoutException TimedOut(string name, TimeSpan timeout) =>
        new(
            $"Another instance held the migration lock '{name}' for longer than {timeout}. It is still migrating, or "
                + "is stuck; the lock is released when its connection closes."
        );
}
