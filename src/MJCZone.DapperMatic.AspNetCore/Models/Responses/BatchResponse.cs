// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

#pragma warning disable SA1402 // A batch's response and its results belong together.
#pragma warning disable SA1649

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// What a batch did: each operation's result, in order.
/// </summary>
public class BatchResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BatchResponse"/> class.
    /// </summary>
    /// <param name="results">Each operation's result.</param>
    /// <param name="dryRun">Whether the writes were rolled back.</param>
    public BatchResponse(IReadOnlyList<BatchOperationResult> results, bool dryRun)
    {
        Results = results;
        DryRun = dryRun;
    }

    /// <summary>Gets each operation's result, in order.</summary>
    public IReadOnlyList<BatchOperationResult> Results { get; }

    /// <summary>Gets a value indicating whether the writes were rolled back.</summary>
    public bool DryRun { get; }
}

/// <summary>
/// What one operation of a batch did.
/// </summary>
public class BatchOperationResult
{
    /// <summary>Gets or sets the number of rows the operation wrote.</summary>
    public int Affected { get; set; }

    /// <summary>Gets or sets an insert's rows as written, keys the database generated among them; null for others.</summary>
    public List<Dictionary<string, object?>>? Rows { get; set; }
}
