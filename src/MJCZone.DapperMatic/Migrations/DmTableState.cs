// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Migrations;

/// <summary>
/// A table's model, its fingerprint, and how that compares with what the ledger recorded.
/// </summary>
public sealed class DmTableState
{
    /// <summary>
    /// Gets the table's model.
    /// </summary>
    public required DmTable Model { get; init; }

    /// <summary>
    /// Gets the table's key in the ledger: its schema-qualified name, with the schema resolved, in lowercase.
    /// </summary>
    public required string TableKey { get; init; }

    /// <summary>
    /// Gets the fingerprint of the model. See <see cref="DmTableFingerprint"/>.
    /// </summary>
    public required string Fingerprint { get; init; }

    /// <summary>
    /// Gets the fingerprint the ledger recorded for the table, or null when nothing is recorded.
    /// </summary>
    public string? RecordedFingerprint { get; init; }

    /// <summary>
    /// Gets how the model compares with what was recorded.
    /// </summary>
    public required DmTableStatus Status { get; init; }

    /// <summary>
    /// Gets what superseded the model, when <see cref="Status"/> is <see cref="DmTableStatus.Superseded"/>.
    /// </summary>
    public string? SupersededBy { get; init; }

    /// <summary>
    /// Gets a value indicating whether the table is reconciled.
    /// </summary>
    public bool NeedsReconcile =>
        Status is DmTableStatus.NotRecorded or DmTableStatus.ModelChanged or DmTableStatus.Forced;

    /// <summary>
    /// Returns the table key and status.
    /// </summary>
    /// <returns>The table key and status.</returns>
    public override string ToString() => $"{TableKey}: {Status}";
}
