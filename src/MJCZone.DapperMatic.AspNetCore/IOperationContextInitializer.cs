// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace MJCZone.DapperMatic.AspNetCore;

/// <summary>
/// Interface for initializing operation context with HTTP-specific information.
/// </summary>
public interface IOperationContextInitializer
{
    /// <summary>
    /// Initializes the operation context with HTTP-specific information.
    /// </summary>
    /// <param name="context">The operation context to initialize.</param>
    /// <param name="httpContext">The HTTP context containing request information.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InitializeAsync(IOperationContext context, HttpContext httpContext);
}
