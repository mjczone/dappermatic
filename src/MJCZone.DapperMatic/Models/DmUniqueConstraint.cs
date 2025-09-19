// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Represents a unique constraint on a table.
/// </summary>
[Serializable]
public class DmUniqueConstraint : DmConstraint
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmUniqueConstraint"/> class.
    /// Used for deserialization.
    /// </summary>
    public DmUniqueConstraint()
        : base(string.Empty) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmUniqueConstraint"/> class.
    /// Used when schema name and table name are not necessary as when creating a table.
    /// </summary>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="columns">The columns.</param>
    [SetsRequiredMembers]
    public DmUniqueConstraint(string constraintName, DmOrderedColumn[] columns)
        : this(null, string.Empty, constraintName, columns) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmUniqueConstraint"/> class.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="columns">The columns.</param>
    [SetsRequiredMembers]
    public DmUniqueConstraint(
        string? schemaName,
        string tableName,
        string constraintName,
        DmOrderedColumn[] columns
    )
        : base(constraintName)
    {
        SchemaName = schemaName;
        TableName = tableName;
        Columns = [.. columns];
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
    /// Gets or sets the columns.
    /// </summary>
    public required List<DmOrderedColumn> Columns { get; set; } = [];

    /// <summary>
    /// Gets the type of the constraint.
    /// </summary>
    public override DmConstraintType ConstraintType => DmConstraintType.Unique;
}
