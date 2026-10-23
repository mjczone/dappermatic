// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore;
using MJCZone.DapperMatic.Security;

var builder = WebApplication.CreateBuilder(
    new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory }
);

// Add DapperMatic services
// DapperMatic bundles no driver: the application registers the ones it uses.
builder.Services.AddDapperMatic(dm =>
    dm.AddProvider(Npgsql.NpgsqlFactory.Instance)
        .AddProvider(Microsoft.Data.SqlClient.SqlClientFactory.Instance)
        .AddProvider(MySqlConnector.MySqlConnectorFactory.Instance)
        .AddProvider(Microsoft.Data.Sqlite.SqliteFactory.Instance)
        // Nothing is allowed unless a policy grants it: most tests are about other things, so everything is granted
        // here, and the policy tests clear it.
        .Policy(DmPolicy.Allow(DmAction.All, "*").Named("tests: everything"))
);
builder.Services.Configure<DapperMaticOptions>(options => options.ConnectionPolicy.SqliteDirectory = Path.GetTempPath());

var app = builder.Build();

app.UseRouting();

// Map DapperMatic endpoints
app.UseDapperMatic();

app.Run();

/// <summary>
/// Test application program for WebApplicationFactory.
/// </summary>
public partial class Program
{
    // This class is used by WebApplicationFactory for testing
}
