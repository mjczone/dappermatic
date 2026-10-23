// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Security;

/// <summary>
/// Narrows a policy, fluently: <c>DmPolicy.Allow(DmAction.ReadWrite, "staff").InSchemas("sales").Where("tenant_id eq
/// @tenant.id")</c>.
/// </summary>
public static class DmPolicyExtensions
{
    /// <summary>Names the policy, as effective permissions show it.</summary>
    /// <param name="policy">The policy to change.</param>
    /// <param name="name">The name.</param>
    /// <returns>The same policy, for chaining.</returns>
    public static DmPolicy Named(this DmPolicy policy, string name)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Name = name;
        return policy;
    }

    /// <summary>Requires a claim of the caller: a value, or <c>*</c> for any.</summary>
    /// <param name="policy">The policy to change.</param>
    /// <param name="type">The claim type.</param>
    /// <param name="value">The value, or <c>*</c>.</param>
    /// <returns>The same policy, for chaining.</returns>
    public static DmPolicy WithClaim(this DmPolicy policy, string type, string value = "*")
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Claims[type] = value;
        return policy;
    }

    /// <summary>
    /// Applies the policy only to requests whose context satisfies a predicate: in code, with the caller, the tenant,
    /// and the request's services for anything else.
    /// </summary>
    /// <param name="policy">The policy to change.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The same policy, for chaining.</returns>
    public static DmPolicy When(this DmPolicy policy, Func<DmAccessContext, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Predicate = predicate;
        return policy;
    }

    /// <summary>Covers only these datasources, by id or pattern with <c>*</c>.</summary>
    /// <param name="policy">The policy to change.</param>
    /// <param name="datasources">The datasources.</param>
    /// <returns>The same policy, for chaining.</returns>
    public static DmPolicy OnDatasources(this DmPolicy policy, params string[] datasources) => Add(policy, policy?.Datasources, datasources);

    /// <summary>Covers only these schemas, by name or pattern; <c>_</c> is the default schema of a request naming none.</summary>
    /// <param name="policy">The policy to change.</param>
    /// <param name="schemas">The schemas.</param>
    /// <returns>The same policy, for chaining.</returns>
    public static DmPolicy InSchemas(this DmPolicy policy, params string[] schemas) => Add(policy, policy?.Schemas, schemas);

    /// <summary>Leaves out these schemas, by name or pattern.</summary>
    /// <param name="policy">The policy to change.</param>
    /// <param name="schemas">The schemas.</param>
    /// <returns>The same policy, for chaining.</returns>
    public static DmPolicy ExceptSchemas(this DmPolicy policy, params string[] schemas) => Add(policy, policy?.ExceptSchemas, schemas);

    /// <summary>Covers only these tables and views, by name or pattern.</summary>
    /// <param name="policy">The policy to change.</param>
    /// <param name="tables">The tables.</param>
    /// <returns>The same policy, for chaining.</returns>
    public static DmPolicy OnTables(this DmPolicy policy, params string[] tables) => Add(policy, policy?.Tables, tables);

    /// <summary>
    /// Covers running only these functions, by name or pattern. A policy naming functions grants
    /// <see cref="DmAction.Execute"/> and nothing else.
    /// </summary>
    /// <param name="policy">The policy to change.</param>
    /// <param name="functions">The functions.</param>
    /// <returns>The same policy, for chaining.</returns>
    public static DmPolicy OnFunctions(this DmPolicy policy, params string[] functions) => Add(policy, policy?.Functions, functions);

    /// <summary>Leaves out these tables and views, by name or pattern.</summary>
    /// <param name="policy">The policy to change.</param>
    /// <param name="tables">The tables.</param>
    /// <returns>The same policy, for chaining.</returns>
    public static DmPolicy ExceptTables(this DmPolicy policy, params string[] tables) => Add(policy, policy?.ExceptTables, tables);

    /// <summary>
    /// Covers, for row actions, only tables having all these columns: one policy for every table with a
    /// <c>tenant_id</c>, in whatever schema.
    /// </summary>
    /// <param name="policy">The policy to change.</param>
    /// <param name="columns">The columns.</param>
    /// <returns>The same policy, for chaining.</returns>
    public static DmPolicy OnTablesWithColumns(this DmPolicy policy, params string[] columns) => Add(policy, policy?.WithColumns, columns);

    /// <summary>Lets callers read and write only these columns.</summary>
    /// <param name="policy">The policy to change.</param>
    /// <param name="columns">The columns.</param>
    /// <returns>The same policy, for chaining.</returns>
    public static DmPolicy OnlyColumns(this DmPolicy policy, params string[] columns) => Add(policy, policy?.Columns, columns);

    /// <summary>Keeps these columns from callers.</summary>
    /// <param name="policy">The policy to change.</param>
    /// <param name="columns">The columns.</param>
    /// <returns>The same policy, for chaining.</returns>
    public static DmPolicy ExceptColumns(this DmPolicy policy, params string[] columns) => Add(policy, policy?.ExcludeColumns, columns);

    /// <summary>
    /// Limits the rows read, updated and deleted, with a condition in the query syntax over the table's own columns and
    /// values of the request: <c>tenant_id eq null or tenant_id in @tenant.below</c>.
    /// </summary>
    /// <param name="policy">The policy to change.</param>
    /// <param name="filter">The condition.</param>
    /// <returns>The same policy, for chaining.</returns>
    public static DmPolicy Where(this DmPolicy policy, string filter)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Filter = filter;
        return policy;
    }

    /// <summary>
    /// Makes a grant of <see cref="DmAction.ManageAccess"/> one for the caller's own tenant: its holders keep policies
    /// that belong to their tenant, held to its rows, giving no more than they may do themselves.
    /// </summary>
    /// <param name="policy">The policy to change.</param>
    /// <returns>The same policy, for chaining.</returns>
    public static DmPolicy WithinOwnTenant(this DmPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.WithinOwnTenant = true;
        return policy;
    }

    /// <summary>Sets a condition every row inserted or updated must satisfy, in the query syntax.</summary>
    /// <param name="policy">The policy to change.</param>
    /// <param name="check">The condition.</param>
    /// <returns>The same policy, for chaining.</returns>
    public static DmPolicy Check(this DmPolicy policy, string check)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Check = check;
        return policy;
    }

    /// <summary>
    /// Writes a column on insert and update whatever the request says: a value of the request's context such as
    /// <c>@tenant.id</c>, or a fixed text.
    /// </summary>
    /// <param name="policy">The policy to change.</param>
    /// <param name="column">The column.</param>
    /// <param name="value">The value.</param>
    /// <returns>The same policy, for chaining.</returns>
    public static DmPolicy Preset(this DmPolicy policy, string column, string? value)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Presets[column] = value;
        return policy;
    }

    private static DmPolicy Add(DmPolicy? policy, List<string>? list, string[] values)
    {
        ArgumentNullException.ThrowIfNull(policy);
        list!.AddRange(values ?? []);
        return policy;
    }
}
