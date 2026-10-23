// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// Each endpoint's operation is named once, when it is mapped: the names permissions, policies and audit events use.
/// </summary>
public class OperationNamesTests
{
    [Fact]
    public void Every_endpoint_is_named_when_mapped()
    {
        using var factory = new WafWithInMemoryDatasourceRepository([]).WithWebHostBuilder(web =>
            web.ConfigureServices(s => s.Configure<DapperMaticOptions>(o => o.Endpoints = DapperMaticEndpoints.All))
        );
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/dm/", StringComparison.Ordinal) == true)
            .ToList();
        endpoints.Should().NotBeEmpty();

        // Every endpoint carries its name; only one answering several methods (MCP) is named per request.
        foreach (var endpoint in endpoints)
        {
            var metadata = endpoint.Metadata.GetMetadata<DapperMaticOperationMetadata>();
            metadata.Should().NotBeNull(endpoint.RoutePattern.RawText);
            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
            if (methods is { Count: 1 })
            {
                metadata!.Name.Should().NotBeNullOrEmpty(endpoint.RoutePattern.RawText);
            }
        }

        // Patterns as mapped: a group's own path with "/" appended reads with a trailing slash.
        string NameOf(string method, string pattern) =>
            endpoints.SingleOrDefault(e => e.RoutePattern.RawText?.TrimEnd('/') == pattern && e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true)
                ?.Metadata.GetMetadata<DapperMaticOperationMetadata>()!.Name
            ?? throw new InvalidOperationException($"No {method} {pattern} among: {string.Join(", ", endpoints.Select(e => e.RoutePattern.RawText))}");

        NameOf("GET", "/api/dm/d/{datasourceId}/t/{tableName}/query").Should().Be("tables/query");
        NameOf("POST", "/api/dm/d/{datasourceId}/t/{tableName}/rows").Should().Be("tables/insert-rows");
        NameOf("POST", "/api/dm/d/{datasourceId}/t/{tableName}/rows/save").Should().Be("tables/save-rows");
        NameOf("POST", "/api/dm/d/{datasourceId}/t/{tableName}/rows/update").Should().Be("tables/update-rows");
        NameOf("POST", "/api/dm/d/{datasourceId}/s/{schemaName}/t/{tableName}/rows/delete").Should().Be("tables/delete-rows");
        NameOf("GET", "/api/dm/d/{datasourceId}/v/{viewName}/subscribe").Should().Be("views/query");
        NameOf("POST", "/api/dm/d/{datasourceId}/t/{tableName}/columns").Should().Be("columns/post");
        NameOf("GET", "/api/dm/d/{datasourceId}/openapi").Should().Be("openapi/list");
        NameOf("POST", "/api/dm/d/{datasourceId}/f/{functionName}").Should().Be("functions/post");
        NameOf("PUT", "/api/dm/access/webhooks/{webhookName}").Should().Be("webhooks/put");
        NameOf("GET", "/api/dm/access/effective").Should().Be("access/effective");
    }
}
