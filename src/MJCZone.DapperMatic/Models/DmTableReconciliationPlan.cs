// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Describes the difference between a table as described by a model and the table as it exists in the
/// database, together with the additive work needed to bring the database in line with the model.
/// </summary>
/// <remarks>
/// Computing a plan never modifies the database. A plan is a snapshot: it reflects the schema as it was
/// when the plan was computed, so applying a stale plan may find that some of its work is already done.
/// Reconciliation is idempotent, so that is harmless, but it is why the apply path recomputes rather
/// than accepting a plan supplied by the caller.
/// </remarks>
public sealed class DmTableReconciliationPlan
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
    /// Gets a value indicating whether the table does not exist in the database.
    /// </summary>
    /// <remarks>
    /// When the table is missing, the individual "missing element" collections are left empty:
    /// creating the table creates every element the model describes in one statement, so listing
    /// them separately would misrepresent the work. Inspect the model itself for that detail.
    /// </remarks>
    public bool TableMissing { get; init; }

    /// <summary>
    /// Gets the element kinds this plan was computed for.
    /// </summary>
    public DmReconcileElements Elements { get; init; } = DmReconcileElements.All;

    /// <summary>
    /// Gets the columns present in the model but missing from the database.
    /// </summary>
    public IReadOnlyList<DmColumn> MissingColumns { get; init; } = [];

    /// <summary>
    /// Gets the primary key constraint to create, or <see langword="null"/> when the table already has
    /// a primary key or the model does not describe one.
    /// </summary>
    public DmPrimaryKeyConstraint? MissingPrimaryKeyConstraint { get; init; }

    /// <summary>
    /// Gets the unique constraints present in the model but missing from the database.
    /// </summary>
    public IReadOnlyList<DmUniqueConstraint> MissingUniqueConstraints { get; init; } = [];

    /// <summary>
    /// Gets the check constraints present in the model but missing from the database.
    /// </summary>
    public IReadOnlyList<DmCheckConstraint> MissingCheckConstraints { get; init; } = [];

    /// <summary>
    /// Gets the default constraints present in the model but missing from the database.
    /// </summary>
    public IReadOnlyList<DmDefaultConstraint> MissingDefaultConstraints { get; init; } = [];

    /// <summary>
    /// Gets the indexes present in the model but missing from the database.
    /// </summary>
    public IReadOnlyList<DmIndex> MissingIndexes { get; init; } = [];

    /// <summary>
    /// Gets the foreign key constraints present in the model but missing from the database.
    /// </summary>
    public IReadOnlyList<DmForeignKeyConstraint> MissingForeignKeyConstraints { get; init; } = [];

    /// <summary>
    /// Gets the changes to existing columns that cannot lose data. Planned only when the plan was asked for safe
    /// alterations; otherwise the same differences appear in <see cref="Drift"/>.
    /// </summary>
    public IReadOnlyList<DmColumnAlteration> Alterations { get; init; } = [];

    /// <summary>
    /// Gets the elements that exist under an earlier name and are renamed instead of created, in the order the
    /// renames are made: the table, then columns, then indexes and constraints.
    /// </summary>
    public IReadOnlyList<DmSchemaRename> Renames { get; init; } = [];

    /// <summary>
    /// Gets the elements that the model describes but that cannot be created yet, because a column
    /// they depend on is absent from the database and is not being added by this plan.
    /// </summary>
    /// <remarks>
    /// This happens when <see cref="Elements"/> excludes <see cref="DmReconcileElements.Columns"/>
    /// while the model has grown a column and an index over it: creating the index alone would fail.
    /// Widening the filter, or reconciling columns first, clears the block.
    /// </remarks>
    public IReadOnlyList<DmSchemaAddition> Blocked { get; init; } = [];

    /// <summary>
    /// Gets the elements that exist on both sides but do not match.
    /// </summary>
    /// <remarks>
    /// Drift is informational. Reconciliation never resolves it, because doing so would mean altering
    /// or replacing an object that may already hold data.
    /// </remarks>
    public IReadOnlyList<DmSchemaDrift> Drift { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether applying this plan would change the database.
    /// </summary>
    public bool HasChanges =>
        TableMissing
        || MissingColumns.Count > 0
        || MissingPrimaryKeyConstraint is not null
        || MissingUniqueConstraints.Count > 0
        || MissingCheckConstraints.Count > 0
        || MissingDefaultConstraints.Count > 0
        || MissingIndexes.Count > 0
        || MissingForeignKeyConstraints.Count > 0
        || Alterations.Count > 0
        || Renames.Count > 0;

    /// <summary>
    /// Gets a value indicating whether any drift was detected between the model and the database.
    /// </summary>
    public bool HasDrift => Drift.Count > 0;

    /// <summary>
    /// Gets a flat, human-readable list of every element this plan would create.
    /// </summary>
    public IReadOnlyList<DmSchemaAddition> Additions
    {
        get
        {
            var additions = new List<DmSchemaAddition>();

            if (TableMissing)
            {
                additions.Add(new DmSchemaAddition(DmSchemaElementType.Table, TableName));
                return additions;
            }

            foreach (var column in MissingColumns)
            {
                additions.Add(new DmSchemaAddition(DmSchemaElementType.Column, column.ColumnName));
            }

            if (MissingPrimaryKeyConstraint is not null)
            {
                additions.Add(
                    new DmSchemaAddition(
                        DmSchemaElementType.PrimaryKeyConstraint,
                        MissingPrimaryKeyConstraint.ConstraintName
                    )
                );
            }

            foreach (var uc in MissingUniqueConstraints)
            {
                additions.Add(new DmSchemaAddition(DmSchemaElementType.UniqueConstraint, uc.ConstraintName));
            }

            foreach (var cc in MissingCheckConstraints)
            {
                additions.Add(new DmSchemaAddition(DmSchemaElementType.CheckConstraint, cc.ConstraintName));
            }

            foreach (var dc in MissingDefaultConstraints)
            {
                additions.Add(new DmSchemaAddition(DmSchemaElementType.DefaultConstraint, dc.ConstraintName));
            }

            foreach (var ix in MissingIndexes)
            {
                additions.Add(new DmSchemaAddition(DmSchemaElementType.Index, ix.IndexName));
            }

            foreach (var fk in MissingForeignKeyConstraints)
            {
                additions.Add(new DmSchemaAddition(DmSchemaElementType.ForeignKeyConstraint, fk.ConstraintName));
            }

            return additions;
        }
    }
}
