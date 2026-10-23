// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models.Dtos;

/// <summary>
/// Represents pagination information for query results.
/// </summary>
public class PaginationDto
{
    /// <summary>
    /// Gets or sets the number of records taken.
    /// </summary>
    public int Take { get; set; }

    /// <summary>
    /// Gets or sets the number of records skipped.
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Gets or sets the total number of records (if requested).
    /// </summary>
    public long? Total { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there are more records available.
    /// </summary>
    public bool HasMore { get; set; }

    /// <summary>
    /// Gets or sets the cursor of the next page, for <c>$after</c>: null on the last page, or for a query that is not
    /// ordered by the table's own columns, or of a table without a key.
    /// </summary>
    public string? NextCursor { get; set; }

    /// <summary>
    /// Gets the current page number (1-based).
    /// </summary>
    public int Page => (Skip / Take) + 1;

    /// <summary>
    /// Gets the total number of pages (if total is available).
    /// </summary>
    public int? TotalPages => Total.HasValue ? (int)Math.Ceiling((double)Total.Value / Take) : null;
}
