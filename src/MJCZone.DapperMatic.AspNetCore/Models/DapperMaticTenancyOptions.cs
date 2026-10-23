// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Models;

/// <summary>
/// Finds the tenant a request acts for in it: its id, from a source; then what is known of the tenant, from
/// configuration or code. Nothing is assumed about what a tenant is: a key on rows, a schema, a datasource, a node of a
/// tree; set what applies.
/// </summary>
/// <remarks>
/// Sources are tried in order, those given in code first, and the first that finds an id wins. The tenant is then
/// completed from <see cref="Tenants"/>, then by each lookup given in code, in order.
/// </remarks>
public sealed class DapperMaticTenancyOptions
{
    /// <summary>Gets where a tenant's id is read from: a claim, a header, a route value, a query value, a subdomain.</summary>
    public List<DapperMaticTenantSource> Sources { get; } = [];

    /// <summary>Gets what is known of each tenant, by id: its datasource, its schema, its values.</summary>
    public Dictionary<string, DapperMaticTenantSettings> Tenants { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets values every tenant has, by name, read as <c>@tenant.name</c>: a fixed value, or one SQL query run when the
    /// value is first read (<c>SELECT id FROM core.tenants WHERE path LIKE @tenant.path || '%'</c>).
    /// </summary>
    public Dictionary<string, DapperMaticValueSettings> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets a value indicating whether a request for a datasource must have a tenant: one without is refused.
    /// Default is false.
    /// </summary>
    public bool RequireTenant { get; set; }

    /// <summary>
    /// Gets or sets the column that marks a tenant's rows, in every table that has it, such as <c>tenant_id</c>. The
    /// policies a tenant's administrator keeps (<see cref="MJCZone.DapperMatic.Security.DmPolicy.WithinOwnTenant"/>)
    /// are held to the rows whose column is the tenant's.
    /// </summary>
    public string? RowsColumn { get; set; }

    /// <summary>Gets the tables whose rows another column marks, by table name, overriding <see cref="RowsColumn"/>.</summary>
    public Dictionary<string, string> RowsColumns { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets a list value of the request naming the tenants whose rows a tenant's administrators reach, such as
    /// <c>@tenant.below</c> for their tenant and those below it; null for their own tenant only.
    /// </summary>
    public string? TenantRulesReach { get; set; }

    /// <summary>Gets the sources given in code, tried before <see cref="Sources"/>.</summary>
    internal List<Func<HttpContext, CancellationToken, ValueTask<object?>>> CodeSources { get; } = [];

    /// <summary>Gets the lookups given in code, which complete a tenant found by id.</summary>
    internal List<Func<DmTenant, HttpContext, CancellationToken, ValueTask>> Lookups { get; } = [];

    /// <summary>Gets the tenant values given in code.</summary>
    internal Dictionary<string, DmValueProvider> CodeValues { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Where a tenant's id is read from. Set one property.
/// </summary>
public sealed class DapperMaticTenantSource
{
    /// <summary>Gets or sets a claim type, such as <c>tenant_id</c>.</summary>
    public string? Claim { get; set; }

    /// <summary>Gets or sets a request header, such as <c>X-Tenant-Id</c>.</summary>
    public string? Header { get; set; }

    /// <summary>Gets or sets a route value, such as <c>tenant</c>.</summary>
    public string? Route { get; set; }

    /// <summary>Gets or sets a query string value, such as <c>tenant</c>.</summary>
    public string? Query { get; set; }

    /// <summary>
    /// Gets or sets a host pattern with a <c>{tenant}</c> placeholder, such as <c>{tenant}.app.example.com</c>.
    /// </summary>
    public string? Subdomain { get; set; }
}

/// <summary>
/// What is known of one tenant.
/// </summary>
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class DapperMaticTenantSettings
{
    /// <summary>Gets or sets the datasource its data is in, or null for none in particular.</summary>
    public string? Datasource { get; set; }

    /// <summary>Gets or sets the schema its tables are in, or null for none in particular.</summary>
    public string? Schema { get; set; }

    /// <summary>Gets its values, read as <c>@tenant.name</c>.</summary>
    public Dictionary<string, string?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// A value: fixed, or read with SQL when first used. Set one of <see cref="Value"/> and <see cref="Sql"/>.
/// </summary>
public sealed class DapperMaticValueSettings
{
    /// <summary>Gets or sets a fixed value.</summary>
    public string? Value { get; set; }

    /// <summary>
    /// Gets or sets a query whose single column is the value: one row gives a value, several a list. It may read
    /// <c>@tenant.id</c>, <c>@tenant.name</c>, <c>@user.id</c>, <c>@claims.type</c> and <c>@values.name</c>, which are
    /// passed as parameters.
    /// </summary>
    public string? Sql { get; set; }

    /// <summary>Gets or sets the datasource the query runs against, or null for the request's.</summary>
    public string? Datasource { get; set; }
}

/// <summary>
/// A value set on each connection's session for the database's own row-level security.
/// </summary>
public sealed class DapperMaticSessionValue
{
    /// <summary>
    /// Gets or sets the name: a dotted custom setting on PostgreSQL (<c>app.tenant_id</c>), a session context key on SQL
    /// Server, a variable name on MySQL.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the value: a path such as <c>@tenant.id</c>, or a fixed text.</summary>
    public string? Value { get; set; }
}
