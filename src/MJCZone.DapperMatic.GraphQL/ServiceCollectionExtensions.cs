// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using GraphQL;
using GraphQL.DI;
using GraphQLParser.AST;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MJCZone.DapperMatic.AspNetCore;
using MJCZone.DapperMatic.GraphQL.Execution;
using MJCZone.DapperMatic.GraphQL.Schema;

namespace MJCZone.DapperMatic.GraphQL;

/// <summary>
/// Adds DapperMatic's GraphQL endpoint.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds DapperMatic's GraphQL endpoint, served by <c>app.UseDapperMatic()</c> at
    /// <c>{BasePath}/d/{datasourceId}/graphql</c>: each caller's own schema of the tables, views and functions they may
    /// use, run through DapperMatic's engine, policies and tenancy. Call it with <c>AddDapperMatic</c>.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="configure">Sets the GraphQL options, or null for the defaults.</param>
    /// <returns>The services, for chaining.</returns>
    public static IServiceCollection AddDapperMaticGraphQL(this IServiceCollection services, Action<DapperMaticGraphQLOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = services.AddOptions<DapperMaticGraphQLOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        if (services.Any(d => d.ServiceType == typeof(GraphQLSchemaCache)))
        {
            return services;
        }

        services.AddGraphQL(graphql => graphql.AddSystemTextJson().AddExecutionStrategy<DapperMaticMutationStrategy>(OperationType.Mutation));
        services.TryAddSingleton<GraphQLSchemaCache>();
        services.AddSingleton<IConfigureExecution, DapperMaticGraphQLExecution>();
        services.AddSingleton<IDapperMaticEndpointContributor, DapperMaticGraphQLContributor>();
        return services;
    }
}
