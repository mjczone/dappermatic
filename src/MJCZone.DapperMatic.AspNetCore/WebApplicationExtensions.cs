// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Endpoints;

namespace MJCZone.DapperMatic.AspNetCore;

/// <summary>
/// Extension methods for configuring DapperMatic minimal API endpoints.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Maps DapperMatic API endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder UseDapperMatic(this IEndpointRouteBuilder app)
    {
        var options =
            app.ServiceProvider.GetService<IOptions<DapperMaticOptions>>()?.Value
            ?? new DapperMaticOptions();
        app.MapDapperMaticDatasourceEndpoints(options.BasePath);
        app.MapDapperMaticSchemaEndpoints(options.BasePath);
        app.MapDapperMaticViewEndpoints(options.BasePath);
        app.MapDapperMaticTableEndpoints(options.BasePath);
        app.MapDapperMaticDataTypeEndpoints(options.BasePath);
        return app;
    }
}
