// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Represents a table in a database.
/// </summary>
[Serializable]
public class DmTable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmTable"/> class.
    /// Used for deserialization.
    /// </summary>
    public DmTable() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmTable"/> class.
    /// </summary>
    /// <param name="schemaName">The schema name of the table.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="columns">The columns of the table.</param>
    /// <param name="primaryKey">The primary key constraint of the table.</param>
    /// <param name="checkConstraints">The check constraints of the table.</param>
    /// <param name="defaultConstraints">The default constraints of the table.</param>
    /// <param name="uniqueConstraints">The unique constraints of the table.</param>
    /// <param name="foreignKeyConstraints">The foreign key constraints of the table.</param>
    /// <param name="indexes">The indexes of the table.</param>
    [SetsRequiredMembers]
    public DmTable(
        string? schemaName,
        string tableName,
        DmColumn[]? columns = null,
        DmPrimaryKeyConstraint? primaryKey = null,
        DmCheckConstraint[]? checkConstraints = null,
        DmDefaultConstraint[]? defaultConstraints = null,
        DmUniqueConstraint[]? uniqueConstraints = null,
        DmForeignKeyConstraint[]? foreignKeyConstraints = null,
        DmIndex[]? indexes = null
    )
    {
        SchemaName = schemaName;
        TableName = tableName;
        Columns = columns == null ? [] : [.. columns];
        PrimaryKeyConstraint = primaryKey;
        CheckConstraints = checkConstraints == null ? [] : [.. checkConstraints];
        DefaultConstraints = defaultConstraints == null ? [] : [.. defaultConstraints];
        UniqueConstraints = uniqueConstraints == null ? [] : [.. uniqueConstraints];
        ForeignKeyConstraints = foreignKeyConstraints == null ? [] : [.. foreignKeyConstraints];
        Indexes = indexes == null ? [] : [.. indexes];

        // set schemaName and tableName on child objects
        foreach (var col in Columns)
        {
            col.SchemaName = schemaName;
            col.TableName = tableName;
        }
        if (PrimaryKeyConstraint != null)
        {
            PrimaryKeyConstraint!.SchemaName = schemaName;
            PrimaryKeyConstraint!.TableName = tableName;
        }
        foreach (var cc in CheckConstraints)
        {
            cc.SchemaName = schemaName;
            cc.TableName = tableName;
        }
        foreach (var dc in DefaultConstraints)
        {
            dc.SchemaName = schemaName;
            dc.TableName = tableName;
        }
        foreach (var uc in UniqueConstraints)
        {
            uc.SchemaName = schemaName;
            uc.TableName = tableName;
        }
        foreach (var fk in ForeignKeyConstraints)
        {
            fk.SchemaName = schemaName;
            fk.TableName = tableName;
        }
        foreach (var ix in Indexes)
        {
            ix.SchemaName = schemaName;
            ix.TableName = tableName;
        }
    }

    /// <summary>
    /// Gets or sets the schema name of the table.
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Gets or sets the name of the table.
    /// </summary>
    public required string TableName { get; set; }

    /// <summary>
    /// Gets or sets the columns of the table.
    /// </summary>
    public List<DmColumn> Columns { get; set; } = [];

    /// <summary>
    /// Gets or sets the primary key constraint of the table.
    /// </summary>
    public DmPrimaryKeyConstraint? PrimaryKeyConstraint { get; set; }

    /// <summary>
    /// Gets or sets the check constraints of the table.
    /// </summary>
    public List<DmCheckConstraint> CheckConstraints { get; set; } = [];

    /// <summary>
    /// Gets or sets the default constraints of the table.
    /// </summary>
    public List<DmDefaultConstraint> DefaultConstraints { get; set; } = [];

    /// <summary>
    /// Gets or sets the unique constraints of the table.
    /// </summary>
    public List<DmUniqueConstraint> UniqueConstraints { get; set; } = [];

    /// <summary>
    /// Gets or sets the foreign key constraints of the table.
    /// </summary>
    public List<DmForeignKeyConstraint> ForeignKeyConstraints { get; set; } = [];

    /// <summary>
    /// Gets or sets the indexes of the table.
    /// </summary>
    public List<DmIndex> Indexes { get; set; } = [];
}
