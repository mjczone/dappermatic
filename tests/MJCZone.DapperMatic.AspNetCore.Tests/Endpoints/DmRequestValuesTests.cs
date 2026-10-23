// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.Dml;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// <see cref="DmRequestValuesExtensions.UseDapperMaticRequestValues"/> gives the DML methods the current request's
/// tenant and user.
/// </summary>
public class DmRequestValuesTests
{
    [Fact]
    public async Task The_tenant_and_user_are_the_current_requests_Async()
    {
        var (tenantBefore, userBefore) = (DmDefaults.TenantId, DmDefaults.User);
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddHttpContextAccessor();

            await using var app = builder.Build();
            app.UseDapperMaticRequestValues(
                tenant: http => http.Request.Headers["X-Tenant"].ToString(),
                user: http => http.Request.Headers["X-User"].ToString()
            );
            app.MapGet("/values", () => $"{DmDefaults.TenantId!()}/{DmDefaults.User!()}");
            await app.StartAsync();

            var client = app.GetTestClient();
            var first = new HttpRequestMessage(HttpMethod.Get, "/values") { Headers = { { "X-Tenant", "t1" }, { "X-User", "ann" } } };
            var second = new HttpRequestMessage(HttpMethod.Get, "/values") { Headers = { { "X-Tenant", "t2" }, { "X-User", "bob" } } };
            (await (await client.SendAsync(first)).Content.ReadAsStringAsync()).Should().Be("t1/ann");
            (await (await client.SendAsync(second)).Content.ReadAsStringAsync()).Should().Be("t2/bob");

            // Outside a request there is none.
            DmDefaults.TenantId!().Should().BeNull();
        }
        finally
        {
            (DmDefaults.TenantId, DmDefaults.User) = (tenantBefore, userBefore);
        }
    }

    // The typed DML methods take the tenant, user and schema DapperMatic's tenancy finds: configured once.
    [Fact]
    public async Task The_tenant_user_and_schema_are_the_callers_Async()
    {
        var (tenantBefore, userBefore, schemaBefore) = (DmDefaults.TenantId, DmDefaults.User, DmDefaults.TenantSchema);
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddDapperMatic(dm => dm.Tenancy(t => t.FromHeader("X-Tenant").Map("acme", schema: "ten_acme")));

            await using var app = builder.Build();
            app.Use((http, next) =>
            {
                http.User = new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("sub", http.Request.Headers["X-User"].ToString())], "Test")
                );
                return next(http);
            });
            app.UseDapperMaticCallerValues();
            app.MapGet("/values", () => $"{DmDefaults.TenantId!()}/{DmDefaults.User!()}/{DmDefaults.TenantSchema!()}");
            await app.StartAsync();

            var client = app.GetTestClient();
            var acme = new HttpRequestMessage(HttpMethod.Get, "/values") { Headers = { { "X-Tenant", "acme" }, { "X-User", "ann" } } };
            var beta = new HttpRequestMessage(HttpMethod.Get, "/values") { Headers = { { "X-Tenant", "beta" }, { "X-User", "bob" } } };
            (await (await client.SendAsync(acme)).Content.ReadAsStringAsync()).Should().Be("acme/ann/ten_acme");
            (await (await client.SendAsync(beta)).Content.ReadAsStringAsync()).Should().Be("beta/bob/");
            DmDefaults.TenantId!().Should().BeNull();
        }
        finally
        {
            (DmDefaults.TenantId, DmDefaults.User, DmDefaults.TenantSchema) = (tenantBefore, userBefore, schemaBefore);
        }
    }

    [Fact]
    public async Task An_application_without_an_http_context_accessor_is_refused_Async()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        FluentActions.Invoking(() => app.UseDapperMaticRequestValues(tenant: _ => "t")).Should().Throw<InvalidOperationException>();
    }
}
