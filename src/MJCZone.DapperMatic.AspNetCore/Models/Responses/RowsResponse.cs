// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// The rows a write inserted, updated or deleted.
/// </summary>
public class RowsResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RowsResponse"/> class.
    /// </summary>
    /// <param name="affected">The number of rows written.</param>
    public RowsResponse(int affected) => Affected = affected;

    /// <summary>
    /// Initializes a new instance of the <see cref="RowsResponse"/> class, with the rows written.
    /// </summary>
    /// <param name="affected">The number of rows written, related ones not counted.</param>
    /// <param name="rows">The rows as written, with their related rows and the keys the database generated; or null.</param>
    [JsonConstructor]
    public RowsResponse(int affected, List<Dictionary<string, object?>>? rows)
    {
        Affected = affected;
        Rows = rows;
    }

    /// <summary>Gets the number of rows written.</summary>
    public int Affected { get; }

    /// <summary>Gets an insert's or a save's rows as written, keys the database generated among them; null for others.</summary>
    public List<Dictionary<string, object?>>? Rows { get; }
}
