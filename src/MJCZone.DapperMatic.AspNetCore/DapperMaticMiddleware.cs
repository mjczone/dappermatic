// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace MJCZone.DapperMatic.AspNetCore;

/// <summary>
/// Middleware that initializes the DapperMatic operation context with HTTP request information.
/// </summary>
public class DapperMaticMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public DapperMaticMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Invokes the middleware to initialize the operation context.
    /// </summary>
    /// <param name="httpContext">The HTTP context for the current request.</param>
    /// <param name="operationContext">The operation context to initialize (injected from DI).</param>
    /// <param name="initializer">The operation context initializer (injected from DI).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(
        HttpContext httpContext,
        IOperationContext operationContext,
        IOperationContextInitializer initializer
    )
    {
        // Initialize the operation context with HTTP-specific information
        await initializer.InitializeAsync(operationContext, httpContext).ConfigureAwait(false);

        // Continue to the next middleware
        await _next(httpContext).ConfigureAwait(false);
    }
}
