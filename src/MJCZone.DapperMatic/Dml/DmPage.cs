// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// A page of rows read in keyset order, and the cursor of the next one.
/// </summary>
/// <typeparam name="T">The model type.</typeparam>
public sealed class DmPage<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmPage{T}"/> class.
    /// </summary>
    /// <param name="items">The rows.</param>
    /// <param name="nextCursor">The cursor of the next page, or null.</param>
    public DmPage(IReadOnlyList<T> items, string? nextCursor)
    {
        Items = items;
        NextCursor = nextCursor;
    }

    /// <summary>Gets the rows, in the query's order.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>
    /// Gets the cursor of the next page, for <see cref="DmQuery{T}.After(string?)"/> or the web API's <c>$after</c>:
    /// null when this page is not full, so that none can follow.
    /// </summary>
    public string? NextCursor { get; }
}
