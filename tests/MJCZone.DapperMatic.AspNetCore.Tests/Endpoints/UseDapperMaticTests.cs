// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// <see cref="WebApplicationExtensions.UseDapperMatic"/> serves the endpoints with or without a call to
/// <c>UseRouting</c> before it, however the application is hosted.
/// </summary>
public class UseDapperMaticTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_web_application_serves_the_endpoints_Async(bool useRoutingFirst)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDapperMatic(Everything);

        await using var app = builder.Build();
        if (useRoutingFirst)
        {
            app.UseRouting();
        }

        app.UseDapperMatic();
        await app.StartAsync();

        var response = await app.GetTestClient().GetAsync("/api/dm/d/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_startup_style_application_serves_the_endpoints_Async(bool useRoutingFirst)
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(web =>
                web.UseTestServer()
                    .ConfigureServices(services => services.AddRouting().AddDapperMatic(Everything))
                    .Configure(app =>
                    {
                        if (useRoutingFirst)
                        {
                            app.UseRouting();
                        }

                        app.UseDapperMatic();
                    })
            )
            .StartAsync();

        var response = await host.GetTestClient().GetAsync("/api/dm/d/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // The operation is read from the endpoint routing chose, wherever the application put routing: a request is never
    // authorized as nothing in particular, which a policy for some tables would cover.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_policy_for_some_tables_grants_nothing_else_however_routing_is_ordered_Async(bool webApplication)
    {
        static void DevTables(DapperMaticConfigurationBuilder dm) => dm.Policy(DmPolicy.Allow(DmAction.All, "*").OnTables("dev_*"));
        HttpResponseMessage response;
        if (webApplication)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddDapperMatic(DevTables);
            await using var app = builder.Build();
            app.UseDapperMatic();
            app.UseRouting();
            await app.StartAsync();
            response = await app.GetTestClient().DeleteAsync("/api/dm/d/anything/t/orders");
        }
        else
        {
            using var host = await new HostBuilder()
                .ConfigureWebHost(web =>
                    web.UseTestServer()
                        .ConfigureServices(services => services.AddRouting().AddDapperMatic(DevTables))
                        .Configure(app => app.UseDapperMatic())
                )
                .StartAsync();
            response = await host.GetTestClient().DeleteAsync("/api/dm/d/anything/t/orders");
        }

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    // These hosts are about routing: everything is granted.
    private static void Everything(DapperMaticConfigurationBuilder dm) => dm.Policy(DmPolicy.Allow(DmAction.All, "*"));
}
