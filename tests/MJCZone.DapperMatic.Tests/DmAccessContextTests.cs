// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// The values policies and session settings read from a request's access context.
/// </summary>
public class DmAccessContextTests
{
    [Fact]
    public async Task Values_are_read_by_path_and_computed_once_Async()
    {
        var user = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "u7"), new Claim(ClaimTypes.Name, "ann"), new Claim("role", "a"), new Claim("role", "b"), new Claim("region", "eu")],
                "Test",
                ClaimTypes.Name,
                "role"
            )
        );
        var computed = 0;
        var context = new DmAccessContext(user) { Tenant = new DmTenant(2) { SchemaName = "t2" } };
        context.Tenant.Values["below"] = (DmValueProvider)((_, _) =>
        {
            computed++;
            return ValueTask.FromResult<object?>(new[] { 2, 3 });
        });
        context.Values["limit"] = 10;

        Assert.Equal(2, await context.GetValueAsync("@tenant.id"));
        Assert.Equal(2, await context.GetValueAsync("tenant"));
        Assert.Equal("t2", await context.GetValueAsync("@tenant.schema"));
        Assert.Equal(new[] { 2, 3 }, await context.GetValueAsync("@tenant.below"));
        Assert.Equal(new[] { 2, 3 }, await context.GetValueAsync("@tenant.below"));
        Assert.Equal(1, computed);
        Assert.Null(await context.GetValueAsync("@tenant.missing"));

        Assert.Equal("u7", await context.GetValueAsync("@user.id"));
        Assert.Equal("ann", await context.GetValueAsync("@user.name"));
        Assert.Equal("eu", await context.GetValueAsync("@claims.region"));
        Assert.Equal(new List<string> { "a", "b" }, await context.GetValueAsync("@claims.role"));
        Assert.Null(await context.GetValueAsync("@claims.nope"));
        Assert.Equal(10, await context.GetValueAsync("@values.limit"));
        await Assert.ThrowsAsync<ArgumentException>(() => context.GetValueAsync("@server.time").AsTask());

        Assert.True(context.IsInRole("authenticated"));
        Assert.False(context.IsInRole("anonymous"));
        Assert.True(context.IsInRole("b"));
        Assert.True(new DmAccessContext().IsInRole("anonymous"));
        Assert.Null(await new DmAccessContext().GetValueAsync("@tenant.id"));
    }
}
