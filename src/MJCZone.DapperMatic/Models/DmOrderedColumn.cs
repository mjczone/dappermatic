// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Represents a column in an ordered list of columns.
/// </summary>
[Serializable]
public class DmOrderedColumn
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmOrderedColumn"/> class.
    /// Used for deserialization.
    /// </summary>
    public DmOrderedColumn() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmOrderedColumn"/> class.
    /// </summary>
    /// <param name="columnName">Name of the column.</param>
    /// <param name="order">Order of the column.</param>
    [SetsRequiredMembers]
    public DmOrderedColumn(string columnName, DmColumnOrder order = DmColumnOrder.Ascending)
    {
        ColumnName = columnName;
        Order = order;
    }

    /// <summary>
    /// Gets or sets the name of the column.
    /// </summary>
    public required string ColumnName { get; set; }

    /// <summary>
    /// Gets or sets the order of the column.
    /// </summary>
    public required DmColumnOrder Order { get; set; }

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString() => ToString(true);

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <param name="includeOrder">if set to <c>true</c> includes the order in the string.</param>
    /// <returns>A string that represents the current object.</returns>
    public string ToString(bool includeOrder) =>
        $"{ColumnName}{(includeOrder ? Order == DmColumnOrder.Descending ? " DESC" : string.Empty : string.Empty)}";

    /// <summary>
    /// Parses a string representation of an ordered column.
    /// </summary>
    /// <param name="input">The string representation of the ordered column (e.g., "ColumnName DESC").</param>
    /// <returns>A <see cref="DmOrderedColumn"/> instance.</returns>
    public static DmOrderedColumn Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Input cannot be null or whitespace.", nameof(input));
        }

        // Split on last space to separate column name and order
        var columnName = input;
        var order = DmColumnOrder.Ascending;

        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lastPart = parts[^1].ToUpperInvariant();
        if (
            parts.Length > 1
            && (
                lastPart.Equals("ASC", StringComparison.OrdinalIgnoreCase)
                || lastPart.Equals("DESC", StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            columnName = string.Join(' ', parts, 0, parts.Length - 1);
            order = lastPart == "DESC" ? DmColumnOrder.Descending : DmColumnOrder.Ascending;
        }
        return new DmOrderedColumn(columnName, order);
    }
}
