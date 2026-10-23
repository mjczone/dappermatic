// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Migrations.Internal;

/// <summary>
/// One row of the ledger: the fingerprint recorded for one table.
/// </summary>
internal sealed class LedgerRow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LedgerRow"/> class.
    /// </summary>
    /// <param name="tableKey">The table's key.</param>
    /// <param name="modelHash">The fingerprint recorded.</param>
    /// <param name="renameHistory">The stored rename history, or null.</param>
    public LedgerRow(string tableKey, string modelHash, string? renameHistory)
    {
        TableKey = tableKey;
        ModelHash = modelHash;
        RenameHistory = renameHistory;
    }

    /// <summary>
    /// Gets the table's key.
    /// </summary>
    public string TableKey { get; }

    /// <summary>
    /// Gets the fingerprint recorded.
    /// </summary>
    public string ModelHash { get; }

    /// <summary>
    /// Gets the stored rename history, or null.
    /// </summary>
    public string? RenameHistory { get; }
}
