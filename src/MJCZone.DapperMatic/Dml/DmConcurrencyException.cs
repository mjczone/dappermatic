// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// The exception thrown when a row written by key with a <c>[DmConcurrencyToken]</c> was changed or deleted since it
/// was read: nothing was written.
/// </summary>
public sealed class DmConcurrencyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmConcurrencyException"/> class.
    /// </summary>
    public DmConcurrencyException()
        : base("The row was changed or deleted since it was read, so it was not written.") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmConcurrencyException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public DmConcurrencyException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmConcurrencyException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The exception that caused it.</param>
    public DmConcurrencyException(string message, Exception innerException)
        : base(message, innerException) { }
}
