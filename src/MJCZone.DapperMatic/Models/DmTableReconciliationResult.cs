// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Describes what a reconciliation actually did.
/// </summary>
public sealed class DmTableReconciliationResult
{
    /// <summary>
    /// Gets the schema name of the table, if the provider supports schemas.
    /// </summary>
    public string? SchemaName { get; init; }

    /// <summary>
    /// Gets the name of the table.
    /// </summary>
    public required string TableName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the table itself was created.
    /// </summary>
    /// <remarks>
    /// When this is <see langword="true"/>, <see cref="Applied"/> holds a single table entry: the table
    /// and everything the model describes were created by one statement.
    /// </remarks>
    public bool TableCreated { get; init; }

    /// <summary>
    /// Gets the elements that were created.
    /// </summary>
    public IReadOnlyList<DmSchemaAddition> Applied { get; init; } = [];

    /// <summary>
    /// Gets the existing columns that were altered. Empty unless the reconciliation was asked for safe
    /// alterations.
    /// </summary>
    public IReadOnlyList<DmColumnAlteration> Altered { get; init; } = [];

    /// <summary>
    /// Gets the elements that were renamed from an earlier name.
    /// </summary>
    public IReadOnlyList<DmSchemaRename> Renamed { get; init; } = [];

    /// <summary>
    /// Gets the elements that could not be created, altered or renamed, each with the reason.
    /// </summary>
    /// <remarks>
    /// The reasons range from a column the element depends on not being added in this call, which reconciling
    /// again with <see cref="DmReconcileElements.Columns"/> clears, to the database not supporting or refusing
    /// the change.
    /// </remarks>
    public IReadOnlyList<DmSchemaAddition> Blocked { get; init; } = [];

    /// <summary>
    /// Gets the drift detected while reconciling.
    /// </summary>
    /// <remarks>
    /// These differences were reported but deliberately not resolved, because resolving them could lose data or
    /// was not asked for.
    /// </remarks>
    public IReadOnlyList<DmSchemaDrift> Drift { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether anything was created, altered or renamed.
    /// </summary>
    public bool Changed => Applied.Count > 0 || Altered.Count > 0 || Renamed.Count > 0;

    /// <summary>
    /// Gets a value indicating whether any drift was detected between the model and the database.
    /// </summary>
    public bool HasDrift => Drift.Count > 0;
}
