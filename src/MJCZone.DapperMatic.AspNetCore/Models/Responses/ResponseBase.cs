// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Generic response model.
/// </summary>
/// <typeparam name="T">The type of the result data.</typeparam>
public abstract class ResponseBase<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseBase{T}"/> class.
    /// </summary>
    protected ResponseBase()
    {
        Success = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseBase{T}"/> class.
    /// </summary>
    /// <param name="result">The result data.</param>
    /// <param name="success">Indicates whether the operation was successful.</param>
    /// <param name="message">An optional message providing additional information.</param>
    protected ResponseBase(T result, bool success = true, string? message = null)
    {
        Result = result;
        Success = success;
        Message = message;
    }

    /// <summary>
    /// Gets or sets the data payload of the response.
    /// </summary>
    public virtual T? Result { get; set; }

    /// <summary>
    /// Gets or sets a message providing additional information about the response.
    /// </summary>
    public virtual string? Message { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the operation was successful.
    /// </summary>
    public virtual bool Success { get; set; }
}
