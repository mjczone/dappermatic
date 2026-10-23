// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Migrations;

/// <summary>
/// Thrown when <see cref="DmMigrationOptions.ThrowOnBlocked"/> is set and a run left tables pending. The work
/// the run did is saved; <see cref="Result"/> says what it was.
/// </summary>
public sealed class DmMigrationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmMigrationException"/> class.
    /// </summary>
    public DmMigrationException() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmMigrationException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public DmMigrationException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmMigrationException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public DmMigrationException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmMigrationException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="result">What the run did.</param>
    public DmMigrationException(string message, DmMigrationResult result)
        : base(message)
    {
        Result = result;
    }

    /// <summary>
    /// Gets what the run did, or null.
    /// </summary>
    public DmMigrationResult? Result { get; }
}
