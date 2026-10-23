// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Migrations;

/// <summary>
/// Something a <see cref="DmMigrator"/> did, reported through <see cref="DmMigrationOptions.Log"/>.
/// </summary>
public sealed class DmMigrationEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmMigrationEvent"/> class.
    /// </summary>
    /// <param name="kind">What happened.</param>
    /// <param name="level">How much it matters.</param>
    /// <param name="message">A description for a log.</param>
    /// <param name="tableKey">The table it concerns, if any.</param>
    public DmMigrationEvent(DmMigrationEventKind kind, DmMigrationEventLevel level, string message, string? tableKey = null)
    {
        Kind = kind;
        Level = level;
        Message = message;
        TableKey = tableKey;
    }

    /// <summary>
    /// Gets what happened.
    /// </summary>
    public DmMigrationEventKind Kind { get; }

    /// <summary>
    /// Gets how much it matters.
    /// </summary>
    public DmMigrationEventLevel Level { get; }

    /// <summary>
    /// Gets a description for a log.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the ledger key of the table the event concerns, or null.
    /// </summary>
    public string? TableKey { get; }

    /// <summary>
    /// Returns the message.
    /// </summary>
    /// <returns>The message.</returns>
    public override string ToString() => Message;
}
