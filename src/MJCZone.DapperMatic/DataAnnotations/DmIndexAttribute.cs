// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.DataAnnotations;

/// <summary>
/// Attribute to define a database index.
/// </summary>
/// <example>
/// [DmIndex(true, "Col1", "Col2")]
/// </example>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Class,
    AllowMultiple = true,
    Inherited = false
)]
public sealed class DmIndexAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmIndexAttribute"/> class.
    /// </summary>
    /// <param name="isUnique">A value indicating whether the index is unique.</param>
    /// <param name="columnNames">The names of the columns included in the index.</param>
    /// <param name="indexName">The name of the index constraint.</param>
    public DmIndexAttribute(
        bool isUnique = false,
        string[]? columnNames = null,
        string? indexName = null
    )
    {
        if (columnNames == null || columnNames.Length == 0)
        {
            throw new ArgumentException(
                "At least one column name is required",
                nameof(columnNames)
            );
        }

        IsUnique = isUnique;
        Columns = columnNames;
        IndexName = indexName;
    }

    /// <summary>
    /// Gets the index name.
    /// </summary>
    public string? IndexName { get; }

    /// <summary>
    /// Gets a value indicating whether the index is unique.
    /// </summary>
    public bool IsUnique { get; }

    /// <summary>
    /// Gets the columns included in the index.
    /// </summary>
    public string[]? Columns { get; }
}
