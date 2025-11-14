// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Represents an index on a table.
/// </summary>
[Serializable]
public class DmIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmIndex"/> class.
    /// Used for deserialization.
    /// </summary>
    public DmIndex() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmIndex"/> class.
    /// Used when schema name and table name are not necessary as when creating a table.
    /// </summary>
    /// <param name="indexName">The index name.</param>
    /// <param name="columns">The columns in the index.</param>
    /// <param name="isUnique">Indicates whether the index is unique.</param>
    [SetsRequiredMembers]
    public DmIndex(string indexName, DmOrderedColumn[] columns, bool isUnique = false)
        : this(null, string.Empty, indexName, columns, isUnique) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmIndex"/> class.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="columns">The columns in the index.</param>
    /// <param name="isUnique">Indicates whether the index is unique.</param>
    [SetsRequiredMembers]
    public DmIndex(
        string? schemaName,
        string tableName,
        string indexName,
        DmOrderedColumn[] columns,
        bool isUnique = false
    )
    {
        SchemaName = schemaName;
        TableName = tableName;
        IndexName = indexName;
        Columns = [.. columns];
        IsUnique = isUnique;
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
    /// Gets or sets the index name.
    /// </summary>
    public required string IndexName { get; set; }

    /// <summary>
    /// Gets or sets the columns.
    /// </summary>
    public required List<DmOrderedColumn> Columns { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the index is unique.
    /// </summary>
    public bool IsUnique { get; set; }
}
