// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

#pragma warning disable SA1402 // A change and what records it belong together.
#pragma warning disable SA1649
#pragma warning disable SA1313 // Positional record parameters are properties, so they are named as properties.

using System.Data;

namespace MJCZone.DapperMatic.Dml;

/// <summary>What a change did to rows.</summary>
internal enum DmChangeAction
{
    /// <summary>Rows were inserted.</summary>
    Insert,

    /// <summary>Rows were updated.</summary>
    Update,

    /// <summary>Rows were deleted.</summary>
    Delete,

    /// <summary>A function wrote to the table: which rows, and how, is not known.</summary>
    Write,
}

/// <summary>
/// A change to a table's rows, as a statement made it: the keys of the rows changed, with any other columns asked for
/// (a tenant column, say).
/// </summary>
/// <param name="SchemaName">The table's schema, or null for the default.</param>
/// <param name="TableName">The table.</param>
/// <param name="Action">What was done.</param>
/// <param name="Keys">The table's key columns, by which <paramref name="Rows"/> name the rows; empty for a table without a key.</param>
/// <param name="Rows">The rows changed: their key columns and the columns asked for; empty when not known.</param>
/// <param name="Count">How many rows were changed, or null when not known.</param>
internal sealed record DmChange(
    string? SchemaName,
    string TableName,
    DmChangeAction Action,
    IReadOnlyList<string> Keys,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    int? Count
);

/// <summary>
/// Hears the changes writes make, in their transaction, before it commits: what it records is committed with them, and
/// rolled back with them.
/// </summary>
internal interface IDmChangeSink
{
    /// <summary>
    /// Tells whether the changes of a table are wanted, and the columns besides its key to read of its changed rows.
    /// </summary>
    /// <param name="schemaName">The table's schema, or null for the default.</param>
    /// <param name="tableName">The table.</param>
    /// <returns>The columns, maybe none; or null when its changes are not wanted.</returns>
    IReadOnlyList<string>? Wants(string? schemaName, string tableName);

    /// <summary>
    /// Records a change, in the write's transaction.
    /// </summary>
    /// <param name="connection">The connection the write ran on.</param>
    /// <param name="transaction">The write's transaction.</param>
    /// <param name="change">The change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task WrittenAsync(IDbConnection connection, IDbTransaction transaction, DmChange change, CancellationToken cancellationToken);
}
