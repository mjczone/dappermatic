// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// How an upsert finds a conflicting row and what it does to it.
/// </summary>
/// <typeparam name="T">The model type.</typeparam>
/// <example>
/// <code>
/// await db.UpsertAsync(stat, new()
/// {
///     OnConflict = s => new { s.Day, s.Page },
///     Update = (current, incoming) => new PageStat { Views = current.Views + incoming.Views, LastSeen = incoming.LastSeen },
///     UpdateWhere = (current, incoming) => incoming.LastSeen > current.LastSeen,
/// });
/// </code>
/// </example>
public sealed class DmUpsertOptions<T>
    where T : class
{
    /// <summary>
    /// Gets the columns a conflicting row has the same values in, <c>u =&gt; u.Email</c> or
    /// <c>s =&gt; new { s.Day, s.Page }</c>; null for the primary key. They must be the primary key or the columns of a
    /// unique constraint or index, which PostgreSQL and SQLite need. MySQL and MariaDB update on a duplicate of any
    /// unique key.
    /// </summary>
    public Expression<Func<T, object?>>? OnConflict { get; init; }

    /// <summary>
    /// Gets the name of the unique constraint or index a conflicting row violates, in place of
    /// <see cref="OnConflict"/>.
    /// </summary>
    public string? OnConflictConstraint { get; init; }

    /// <summary>
    /// Gets the columns to set on a conflicting row, from the row as it is and the row being upserted:
    /// <c>(current, incoming) =&gt; new PageStat { Views = current.Views + incoming.Views }</c>. Null sets every column
    /// but the key and the conflict columns to the incoming row's values.
    /// </summary>
    public Expression<Func<T, T, T>>? Update { get; init; }

    /// <summary>
    /// Gets a condition a conflicting row is updated only when it holds:
    /// <c>(current, incoming) =&gt; incoming.Version &gt; current.Version</c>. When it does not, the row is left as it is.
    /// </summary>
    public Expression<Func<T, T, bool>>? UpdateWhere { get; init; }
}
