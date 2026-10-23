// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// A response of the access endpoints.
/// </summary>
/// <typeparam name="T">The type of the result.</typeparam>
public class AccessResponse<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AccessResponse{T}"/> class.
    /// </summary>
    /// <param name="result">The result.</param>
    public AccessResponse(T result) => Result = result;

    /// <summary>Gets the result.</summary>
    public T Result { get; }
}
