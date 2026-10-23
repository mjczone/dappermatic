// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Security;

/// <summary>
/// Finding a request's tenant from its sources, completing it, and holding the request to it.
/// </summary>
public class DapperMaticAccessTests
{
    [Fact]
    public async Task A_tenant_is_found_by_the_first_source_that_has_it_and_completed_Async()
    {
        var options = new DapperMaticOptions();
        new DapperMaticTenancyBuilder(options.Tenancy)
            .FromClaim("tenant_id")
            .FromHeader("X-Tenant")
            .FromSubdomain("{tenant}.app.example.com")
            .Map("acme", datasource: "acme-db", schema: "acme")
            .Lookup((tenant, http, _) =>
            {
                tenant.Values["region"] = "eu";
                return ValueTask.CompletedTask;
            })
            .Value("below", (_, _) => ValueTask.FromResult<object?>(new[] { 1, 2 }));
        options.Tenancy.Values["plan"] = new DapperMaticValueSettings { Value = "gold" };

        async Task<MJCZone.DapperMatic.Security.DmAccessContext> Resolve(Action<DefaultHttpContext> request)
        {
            var http = new DefaultHttpContext();
            request(http);
            return (await new DapperMaticAccess(Options.Create(options)).BuildAsync(http)).Access;
        }

        var bySubdomain = await Resolve(http => http.Request.Host = new HostString("acme.app.example.com"));
        bySubdomain.Tenant!.Id.Should().Be("acme");
        (bySubdomain.Tenant.DatasourceId, bySubdomain.Tenant.SchemaName).Should().Be(("acme-db", "acme"));
        (await bySubdomain.GetValueAsync("@tenant.region")).Should().Be("eu");
        (await bySubdomain.GetValueAsync("@tenant.plan")).Should().Be("gold");
        (await bySubdomain.GetValueAsync("@tenant.below")).Should().BeEquivalentTo(new[] { 1, 2 });

        // The claim comes first, then the header.
        var byClaim = await Resolve(http =>
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant_id", "beta")], "Test"));
            http.Request.Headers["X-Tenant"] = "gamma";
            http.Request.Host = new HostString("acme.app.example.com");
        });
        byClaim.Tenant!.Id.Should().Be("beta");
        byClaim.Tenant.SchemaName.Should().BeNull();
        (await Resolve(http => http.Request.Headers["X-Tenant"] = "gamma")).Tenant!.Id.Should().Be("gamma");
        (await Resolve(http => http.Request.Host = new HostString("www.example.com"))).Tenant.Should().BeNull();
    }

    [Fact]
    public async Task A_request_is_held_to_its_tenants_datasource_and_schema_Async()
    {
        var options = new DapperMaticOptions();
        new DapperMaticTenancyBuilder(options.Tenancy).FromHeader("X-Tenant").Map("acme", datasource: "acme-db", schema: "acme");

        async Task<DapperMaticOperation> Enforce(string? tenant, string datasource, string? schema)
        {
            var http = new DefaultHttpContext();
            if (tenant is not null)
            {
                http.Request.Headers["X-Tenant"] = tenant;
            }

            http.Request.RouteValues["datasourceId"] = datasource;
            if (schema is not null)
            {
                http.Request.RouteValues["schemaName"] = schema;
            }

            var access = new DapperMaticAccess(Options.Create(options));
            return access.Hold(await access.BuildAsync(http), new DapperMaticOperation { DatasourceId = datasource, SchemaName = schema });
        }

        (await Enforce("acme", "acme-db", null)).SchemaName.Should().Be("acme");
        (await Enforce("acme", "acme-db", "_")).SchemaName.Should().Be("acme");
        (await Enforce("acme", "ACME-DB", "Acme")).SchemaName.Should().Be("Acme");
        await FluentActions.Awaiting(() => Enforce("acme", "other-db", null)).Should().ThrowAsync<UnauthorizedAccessException>();
        await FluentActions.Awaiting(() => Enforce("acme", "acme-db", "public")).Should().ThrowAsync<UnauthorizedAccessException>();

        // A tenant that is only a key on rows names no datasource or schema, and is held to none.
        (await Enforce("beta", "any-db", "public")).SchemaName.Should().Be("public");
        (await Enforce(null, "any-db", null)).Access!.Tenant.Should().BeNull();

        options.Tenancy.RequireTenant = true;
        await FluentActions.Awaiting(() => Enforce(null, "any-db", null)).Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // One caller's session runs many operations, side by side on a socket: each keeps its own datasource and schema, and
    // a value read for a datasource (by SQL, say) is read for each apart.
    [Fact]
    public async Task Operations_of_one_session_keep_their_own_datasource_schema_and_values_Async()
    {
        var options = new DapperMaticOptions();
        new DapperMaticTenancyBuilder(options.Tenancy)
            .FromHeader("X-Tenant")
            .Value("where", (context, _) => ValueTask.FromResult<object?>(context.DatasourceId));
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant"] = "acme";
        var access = new DapperMaticAccess(Options.Create(options));
        var session = await access.BuildAsync(http);

        var east = access.Hold(session, new DapperMaticOperation { DatasourceId = "east", SchemaName = "sales" });
        var west = access.Hold(session, new DapperMaticOperation { DatasourceId = "west", SchemaName = "hr" });

        (east.DatasourceId, east.SchemaName, west.DatasourceId, west.SchemaName).Should().Be(("east", "sales", "west", "hr"));
        (await east.Access!.GetValueAsync("@tenant.where")).Should().Be("east");
        (await west.Access!.GetValueAsync("@tenant.where")).Should().Be("west");
        (await east.Access!.GetValueAsync("@tenant.where")).Should().Be("east");
        session.Access.DatasourceId.Should().BeNull();
        east.Access.Tenant.Should().BeSameAs(session.Tenant);
    }

    // A stream or socket ends with its caller's session: at the token's expiry, or at the maximum age, sooner of the two.
    [Fact]
    public async Task A_session_ends_at_its_tokens_expiry_or_its_maximum_age_Async()
    {
        var options = new DapperMaticOptions { MaxSessionAge = TimeSpan.FromHours(1) };
        var access = new DapperMaticAccess(Options.Create(options));
        var exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("exp", exp.ToString(System.Globalization.CultureInfo.InvariantCulture))], "Test")),
        };
        var session = await access.BuildAsync(http);
        session.ExpiresAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(exp));
        (await session.CheckAsync()).Should().BeSameAs(session);

        var unsigned = await access.BuildAsync(new DefaultHttpContext());
        unsigned.ExpiresAt.Should().BeCloseTo(unsigned.StartedAt.AddHours(1), TimeSpan.FromSeconds(1));

        options.MaxSessionAge = TimeSpan.Zero;
        var ended = await access.BuildAsync(new DefaultHttpContext());
        await FluentActions.Awaiting(() => ended.CheckAsync().AsTask()).Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
