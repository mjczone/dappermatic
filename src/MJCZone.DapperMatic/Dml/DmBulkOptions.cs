// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// How rows are loaded in bulk.
/// </summary>
public sealed class DmBulkOptions
{
    /// <summary>
    /// Gets the most rows sent to the server at a time: SQL Server's bulk copy batch size, and the rows per statement
    /// of the multi-row <c>VALUES</c> fallback. 0 leaves it to the provider.
    /// </summary>
    public int BatchSize { get; init; }

    /// <summary>
    /// Gets how long a bulk load may take, or null for no limit. SQL Server's bulk copy stops after 30 seconds unless
    /// told otherwise, which a large load easily exceeds.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets what to do when the provider's native bulk path is unavailable: MySql.Data has none, and MySqlConnector's
    /// needs <c>AllowLoadLocalInfile=true</c> and the server's <c>local_infile</c>.
    /// </summary>
    public DmBulkFallback Fallback { get; init; } = DmBulkFallback.MultiRowValues;
}

/// <summary>
/// What a bulk load does when the provider's native bulk path is unavailable.
/// </summary>
public enum DmBulkFallback
{
    /// <summary>Load with multi-row <c>INSERT ... VALUES</c> statements, as <c>InsertManyAsync</c> does.</summary>
    MultiRowValues,

    /// <summary>Throw a <see cref="NotSupportedException"/> that says why the native path is unavailable.</summary>
    Throw,
}
