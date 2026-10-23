// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore;

/// <summary>
/// Configures how a request's tenant is found and completed, in code. Sources given here are tried before those in
/// configuration, in the order given; the first that finds an id wins.
/// </summary>
public sealed class DapperMaticTenancyBuilder
{
    private readonly DapperMaticTenancyOptions _tenancy;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticTenancyBuilder"/> class.
    /// </summary>
    /// <param name="tenancy">The tenancy options it configures.</param>
    internal DapperMaticTenancyBuilder(DapperMaticTenancyOptions tenancy)
    {
        _tenancy = tenancy;
    }

    /// <summary>Reads the tenant's id from a claim of the signed-in caller.</summary>
    /// <param name="claimType">The claim type, such as <c>tenant_id</c>.</param>
    /// <returns>The builder, for chaining.</returns>
    public DapperMaticTenancyBuilder FromClaim(string claimType) => From(new DapperMaticTenantSource { Claim = claimType });

    /// <summary>
    /// Reads the tenant's id from a request header. A client chooses its headers: use one only where something else
    /// authenticates the tenant, such as a gateway, or where policies check the caller's claims too.
    /// </summary>
    /// <param name="header">The header, such as <c>X-Tenant-Id</c>.</param>
    /// <returns>The builder, for chaining.</returns>
    public DapperMaticTenancyBuilder FromHeader(string header) => From(new DapperMaticTenantSource { Header = header });

    /// <summary>Reads the tenant's id from a route value of the application's own routes.</summary>
    /// <param name="name">The route value, such as <c>tenant</c>.</param>
    /// <returns>The builder, for chaining.</returns>
    public DapperMaticTenancyBuilder FromRoute(string name) => From(new DapperMaticTenantSource { Route = name });

    /// <summary>Reads the tenant's id from the query string. The same caution as for headers applies.</summary>
    /// <param name="name">The query value, such as <c>tenant</c>.</param>
    /// <returns>The builder, for chaining.</returns>
    public DapperMaticTenancyBuilder FromQuery(string name) => From(new DapperMaticTenantSource { Query = name });

    /// <summary>Reads the tenant's id from the request's host.</summary>
    /// <param name="pattern">The host, with <c>{tenant}</c> where the id is: <c>{tenant}.app.example.com</c>.</param>
    /// <returns>The builder, for chaining.</returns>
    public DapperMaticTenancyBuilder FromSubdomain(string pattern) => From(new DapperMaticTenantSource { Subdomain = pattern });

    /// <summary>
    /// Finds the tenant's id in code: from a session, a cookie, a lookup of the caller, anything; null when the request
    /// has none. The request's services are <c>http.RequestServices</c>.
    /// </summary>
    /// <param name="source">Finds the id.</param>
    /// <returns>The builder, for chaining.</returns>
    public DapperMaticTenancyBuilder From(Func<HttpContext, CancellationToken, ValueTask<object?>> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _tenancy.CodeSources.Add(source);
        return this;
    }

    /// <summary>
    /// Says what is known of a tenant: its datasource, its schema and its values. The same as an entry of
    /// <c>Tenancy:Tenants</c> in configuration.
    /// </summary>
    /// <param name="id">The tenant's id.</param>
    /// <param name="datasource">Its datasource, or null for none in particular.</param>
    /// <param name="schema">Its schema, or null for none in particular.</param>
    /// <returns>The builder, for chaining.</returns>
    public DapperMaticTenancyBuilder Map(string id, string? datasource = null, string? schema = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        _tenancy.Tenants[id] = new DapperMaticTenantSettings { Datasource = datasource, Schema = schema };
        return this;
    }

    /// <summary>
    /// Completes a tenant found by id in code: sets its datasource, schema or values from wherever the application
    /// keeps them. Lookups run in order, after configuration's entry for the tenant is applied.
    /// </summary>
    /// <param name="lookup">Completes the tenant.</param>
    /// <returns>The builder, for chaining.</returns>
    public DapperMaticTenancyBuilder Lookup(Func<DmTenant, HttpContext, CancellationToken, ValueTask> lookup)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        _tenancy.Lookups.Add(lookup);
        return this;
    }

    /// <summary>
    /// Adds a value every tenant has, read as <c>@tenant.name</c>, computed when first read in a request: the ids of
    /// the tenants below it, its path in a tree.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="provider">Computes the value, given the request's access context.</param>
    /// <returns>The builder, for chaining.</returns>
    public DapperMaticTenancyBuilder Value(string name, DmValueProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(provider);
        _tenancy.CodeValues[name] = provider;
        return this;
    }

    /// <summary>
    /// Adds a value every tenant has, read with one SQL query when first used: one row gives a value, several a list.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="sql">The query, which may read <c>@tenant.id</c> and the other values as parameters.</param>
    /// <param name="datasource">The datasource it runs against, or null for the request's.</param>
    /// <returns>The builder, for chaining.</returns>
    public DapperMaticTenancyBuilder ValueFromSql(string name, string sql, string? datasource = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        _tenancy.Values[name] = new DapperMaticValueSettings { Sql = sql, Datasource = datasource };
        return this;
    }

    /// <summary>Refuses a request for a datasource that has no tenant.</summary>
    /// <param name="required">Whether a tenant is required.</param>
    /// <returns>The builder, for chaining.</returns>
    public DapperMaticTenancyBuilder RequireTenant(bool required = true)
    {
        _tenancy.RequireTenant = required;
        return this;
    }

    /// <summary>
    /// Names the column that marks a tenant's rows, in every table that has it: the policies a tenant's administrator
    /// keeps are held to the rows whose column is the tenant's.
    /// </summary>
    /// <param name="column">The column, such as <c>tenant_id</c>.</param>
    /// <returns>The builder, for chaining.</returns>
    public DapperMaticTenancyBuilder RowsMarkedBy(string column)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(column);
        _tenancy.RowsColumn = column;
        return this;
    }

    /// <summary>Names the column that marks a tenant's rows in one table, where it is not the usual one.</summary>
    /// <param name="table">The table.</param>
    /// <param name="column">The column, such as <c>org_id</c>.</param>
    /// <returns>The builder, for chaining.</returns>
    public DapperMaticTenancyBuilder RowsMarkedBy(string table, string column)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        ArgumentException.ThrowIfNullOrWhiteSpace(column);
        _tenancy.RowsColumns[table] = column;
        return this;
    }

    /// <summary>
    /// Lets the policies a tenant's administrator keeps reach the rows of the tenants a list value of the request names,
    /// such as <c>@tenant.below</c> for their tenant and those below it, rather than their own tenant's only.
    /// </summary>
    /// <param name="value">The value, such as <c>@tenant.below</c>.</param>
    /// <returns>The builder, for chaining.</returns>
    public DapperMaticTenancyBuilder TenantRulesReach(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _tenancy.TenantRulesReach = value;
        return this;
    }

    private DapperMaticTenancyBuilder From(DapperMaticTenantSource source) =>
        From((http, _) => ValueTask.FromResult<object?>(DapperMaticAccess.Read(source, http)));
}
