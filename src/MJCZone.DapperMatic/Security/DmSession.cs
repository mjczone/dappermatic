// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Dapper;

namespace MJCZone.DapperMatic.Security;

/// <summary>
/// Sets values on a connection's session, for the database's own row-level security to read:
/// on PostgreSQL with <c>set_config</c>, read by policies as <c>current_setting('app.tenant_id', true)</c>;
/// on SQL Server with <c>sp_set_session_context</c>, read-only, read as <c>SESSION_CONTEXT(N'tenant_id')</c>;
/// on MySQL and MariaDB as user variables, read as <c>@tenant_id</c>, which needs <c>AllowUserVariables=True</c> in the
/// connection string (MySQL views cannot read variables; a function that returns one can).
/// </summary>
/// <remarks>
/// The values live as long as the session: each driver's pool resets them when the connection is returned (Npgsql's
/// DISCARD ALL, SQL Server's sp_reset_connection, MySqlConnector's connection reset), unless that reset is turned off
/// in the connection string. A list is set as its values joined by commas.
/// </remarks>
public static partial class DmSession
{
    /// <summary>
    /// Sets values on the connection's session, opening it if it is closed.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="values">The values, by name: <c>app.tenant_id</c> on PostgreSQL (a dotted name, as custom settings
    /// require), any name on SQL Server, a variable name on MySQL.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    /// <exception cref="ArgumentException">A name is not one the provider accepts.</exception>
    /// <exception cref="NotSupportedException">The provider has no session values (SQLite).</exception>
    public static async Task SetAsync(
        IDbConnection db,
        IEnumerable<KeyValuePair<string, object?>> values,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(values);
        var list = values.ToList();
        if (list.Count == 0)
        {
            return;
        }

        if (db.State != ConnectionState.Open)
        {
            db.Open();
        }

        var provider = db.GetDbProviderType();
        if (provider == DbProviderType.MySql && !AllowsUserVariables(db.ConnectionString))
        {
            throw new InvalidOperationException(
                "MySQL session values are user variables, which both MySQL drivers refuse unless the connection string has AllowUserVariables=True."
            );
        }

        foreach (var (name, value) in list)
        {
            var text = Text(value);
            var sql = provider switch
            {
                DbProviderType.PostgreSql when PostgreSqlName().IsMatch(name) => "SELECT set_config(@name, COALESCE(@value, ''), false)",
                DbProviderType.PostgreSql => throw new ArgumentException(
                    $"'{name}' is not a PostgreSQL custom setting: name it with a prefix, as app.tenant_id.",
                    nameof(values)
                ),
                DbProviderType.SqlServer when name.Length is > 0 and <= 128 => "EXEC sp_set_session_context @key = @name, @value = @value, @read_only = 1",
                DbProviderType.SqlServer => throw new ArgumentException($"'{name}' is not a session context key.", nameof(values)),
                DbProviderType.MySql when MySqlName().IsMatch(name) => $"SET @{name} = @value",
                DbProviderType.MySql => throw new ArgumentException($"'{name}' is not a MySQL variable name.", nameof(values)),
                _ => throw new NotSupportedException($"{provider} has no session values: SQLite has no sessions to keep them in."),
            };

            await db.ExecuteAsync(new CommandDefinition(sql, new { name, value = text }, tx, cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes a value as the session holds it: text, with a list joined by commas; null stays null. Results kept per
    /// session values are keyed by this text, the one the database reads.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The text, or null.</returns>
    internal static string? Text(object? value) =>
        value switch
        {
            null => null,
            string s => s,
            IEnumerable list => string.Join(',', list.Cast<object?>().Select(v => Convert.ToString(v, CultureInfo.InvariantCulture))),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };

    private static bool AllowsUserVariables(string connectionString)
    {
        var builder = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connectionString };
        return builder.Keys.Cast<string>()
            .Where(k => k.Replace(" ", string.Empty, StringComparison.Ordinal).Equals("AllowUserVariables", StringComparison.OrdinalIgnoreCase))
            .Any(k => string.Equals(Convert.ToString(builder[k], CultureInfo.InvariantCulture), "true", StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)+$")]
    private static partial Regex PostgreSqlName();

    [GeneratedRegex(@"^[A-Za-z0-9_.$]{1,64}$")]
    private static partial Regex MySqlName();
}
