// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace MJCZone.DapperMatic.AspNetCore;

/// <summary>
/// Adds endpoints to DapperMatic's: how a package built on DapperMatic (GraphQL) serves its endpoints in DapperMatic's
/// route group, so that they go through the same request pipeline (the caller's session, tracing, errors, audit) and
/// the conventions the application gives the group. Registered as a service; <see cref="WebApplicationExtensions.UseDapperMatic"/>
/// calls every one.
/// </summary>
public interface IDapperMaticEndpointContributor
{
    /// <summary>
    /// Adds what the contributor's endpoints need to the application's pipeline, before DapperMatic's endpoints are
    /// mapped: <c>UseWebSockets</c>, say. Nothing by default.
    /// </summary>
    /// <param name="app">The application builder.</param>
    void Configure(IApplicationBuilder app)
    {
    }

    /// <summary>
    /// Maps the contributor's endpoints into DapperMatic's route group.
    /// </summary>
    /// <param name="group">The group, under <see cref="DapperMaticOptions.BasePath"/>.</param>
    /// <param name="options">The options.</param>
    void Map(RouteGroupBuilder group, DapperMaticOptions options);
}
