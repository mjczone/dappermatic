// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// The rows a function returned.
/// </summary>
public class FunctionResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionResponse"/> class.
    /// </summary>
    /// <param name="result">The rows, as column name to value.</param>
    public FunctionResponse(List<Dictionary<string, object?>> result) => Result = result;

    /// <summary>Gets the rows, as column name to value; empty when the function returns none.</summary>
    public List<Dictionary<string, object?>> Result { get; }
}
