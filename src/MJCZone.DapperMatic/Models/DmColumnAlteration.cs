// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// A change to an existing column that cannot lose data: a longer length, more precision without losing a
/// digit, or allowing nulls.
/// </summary>
/// <remarks>
/// Reconciliation plans these only when asked to, with its <c>applySafeAlterations</c> parameter. Otherwise the
/// same differences are reported as drift.
/// </remarks>
public sealed class DmColumnAlteration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmColumnAlteration"/> class.
    /// </summary>
    /// <param name="columnName">The column to alter.</param>
    /// <param name="sqlType">The column's data type after the change.</param>
    /// <param name="isNullable">Whether the column allows nulls after the change.</param>
    /// <param name="changes">What changes, in words, for example "length 50 to 200".</param>
    public DmColumnAlteration(string columnName, string sqlType, bool isNullable, IReadOnlyList<string> changes)
    {
        ColumnName = columnName;
        SqlType = sqlType;
        IsNullable = isNullable;
        Changes = changes;
    }

    /// <summary>
    /// Gets the column to alter.
    /// </summary>
    public string ColumnName { get; }

    /// <summary>
    /// Gets the column's data type after the change, spelled the way the database reports it.
    /// </summary>
    public string SqlType { get; }

    /// <summary>
    /// Gets a value indicating whether the column allows nulls after the change.
    /// </summary>
    public bool IsNullable { get; }

    /// <summary>
    /// Gets what changes, in words, for example "length 50 to 200".
    /// </summary>
    public IReadOnlyList<string> Changes { get; }

    /// <summary>
    /// Returns a description of the alteration.
    /// </summary>
    /// <returns>A description of the alteration.</returns>
    public override string ToString()
    {
        return $"Column '{ColumnName}': {string.Join("; ", Changes)}";
    }
}
