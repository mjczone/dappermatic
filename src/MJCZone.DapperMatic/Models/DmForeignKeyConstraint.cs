// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Represents a foreign key constraint in a database.
/// </summary>
[Serializable]
public class DmForeignKeyConstraint : DmConstraint
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmForeignKeyConstraint"/> class.
    /// Used for deserialization.
    /// </summary>
    public DmForeignKeyConstraint()
        : base(string.Empty) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmForeignKeyConstraint"/> class.
    /// Used when schema name and table name are not necessary as when creating a table.
    /// </summary>
    /// <param name="constraintName">The desired name for the new foreign key constraint.</param>
    /// <param name="sourceColumns">An array of DmOrderedColumn objects representing the columns in the source table that are part of the foreign key.</param>
    /// <param name="referencedTableName">The name of the table that is referenced by the foreign key constraint.</param>
    /// <param name="referencedColumns">An array of DmOrderedColumn objects representing the columns in the referenced table that correspond to the source columns.</param>
    /// <param name="onDelete">
    ///     The action to take when a row in the referenced table is deleted. Defaults to <see cref="DmForeignKeyAction.NoAction"/>.
    /// </param>
    /// <param name="onUpdate">
    ///     The action to take when a row in the referenced table is updated. Defaults to <see cref="DmForeignKeyAction.NoAction"/>.
    /// </param>
    [SetsRequiredMembers]
    public DmForeignKeyConstraint(
        string constraintName,
        DmOrderedColumn[] sourceColumns,
        string referencedTableName,
        DmOrderedColumn[] referencedColumns,
        DmForeignKeyAction onDelete = DmForeignKeyAction.NoAction,
        DmForeignKeyAction onUpdate = DmForeignKeyAction.NoAction
    )
        : this(
            null,
            string.Empty,
            constraintName,
            sourceColumns,
            referencedTableName,
            referencedColumns,
            onDelete,
            onUpdate
        ) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmForeignKeyConstraint"/> class.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the table with the foreign key constraint. Can be null if not specified, indicating the default schema.</param>
    /// <param name="tableName">The name of the table that contains the foreign key constraint.</param>
    /// <param name="constraintName">The desired name for the new foreign key constraint.</param>
    /// <param name="sourceColumns">An array of DmOrderedColumn objects representing the columns in the source table that are part of the foreign key.</param>
    /// <param name="referencedTableName">The name of the table that is referenced by the foreign key constraint.</param>
    /// <param name="referencedColumns">An array of DmOrderedColumn objects representing the columns in the referenced table that correspond to the source columns.</param>
    /// <param name="onDelete">
    ///     The action to take when a row in the referenced table is deleted. Defaults to <see cref="DmForeignKeyAction.NoAction"/>.
    /// </param>
    /// <param name="onUpdate">
    ///     The action to take when a row in the referenced table is updated. Defaults to <see cref="DmForeignKeyAction.NoAction"/>.
    /// </param>
    [SetsRequiredMembers]
    public DmForeignKeyConstraint(
        string? schemaName,
        string tableName,
        string constraintName,
        DmOrderedColumn[] sourceColumns,
        string referencedTableName,
        DmOrderedColumn[] referencedColumns,
        DmForeignKeyAction onDelete = DmForeignKeyAction.NoAction,
        DmForeignKeyAction onUpdate = DmForeignKeyAction.NoAction
    )
        : base(constraintName)
    {
        if (sourceColumns.Length != referencedColumns.Length)
        {
            throw new ArgumentException("SourceColumns and ReferencedColumns must have the same number of columns.");
        }

        SchemaName = schemaName;
        TableName = tableName;
        SourceColumns = [.. sourceColumns];
        ReferencedTableName = referencedTableName;
        ReferencedColumns = [.. referencedColumns];
        OnDelete = onDelete;
        OnUpdate = onUpdate;
    }

    /// <summary>
    /// Gets or sets the schema name.
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Gets or sets the table name.
    /// </summary>
    public required string TableName { get; set; }

    /// <summary>
    /// Gets or sets the source columns.
    /// </summary>
    public required List<DmOrderedColumn> SourceColumns { get; set; } = [];

    /// <summary>
    /// Gets or sets the referenced table name.
    /// </summary>
    public required string ReferencedTableName { get; set; }

    /// <summary>
    /// Gets or sets the referenced columns.
    /// </summary>
    public required List<DmOrderedColumn> ReferencedColumns { get; set; } = [];

    /// <summary>
    /// Gets or sets the action on delete.
    /// </summary>
    public DmForeignKeyAction OnDelete { get; set; } = DmForeignKeyAction.NoAction;

    /// <summary>
    /// Gets or sets the action on update.
    /// </summary>
    public DmForeignKeyAction OnUpdate { get; set; } = DmForeignKeyAction.NoAction;

    /// <summary>
    /// Gets the constraint type.
    /// </summary>
    public override DmConstraintType ConstraintType => DmConstraintType.ForeignKey;
}
