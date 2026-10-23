// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Migrations.Internal;

/// <summary>
/// One reconcile that renamed something: what it renamed, and the fingerprint of the model it replaced.
/// </summary>
internal sealed class RenameHistoryEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RenameHistoryEntry"/> class.
    /// </summary>
    /// <param name="supersedes">The fingerprint recorded before the renames, or null when none was.</param>
    /// <param name="at">When the renames were made, in UTC.</param>
    /// <param name="renames">The renames, described.</param>
    public RenameHistoryEntry(string? supersedes, DateTime at, IReadOnlyList<string> renames)
    {
        Supersedes = supersedes;
        At = at;
        Renames = renames;
    }

    /// <summary>
    /// Gets the fingerprint recorded before the renames, or null when none was. A model with this fingerprint
    /// is superseded.
    /// </summary>
    public string? Supersedes { get; }

    /// <summary>
    /// Gets when the renames were made, in UTC.
    /// </summary>
    public DateTime At { get; }

    /// <summary>
    /// Gets the renames, described.
    /// </summary>
    public IReadOnlyList<string> Renames { get; }
}
