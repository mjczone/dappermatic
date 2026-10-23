// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

#pragma warning disable SA1402 // A function and its parameters belong together.

namespace MJCZone.DapperMatic.AspNetCore.Models;

/// <summary>
/// A function callers may run through <c>POST /d/{datasourceId}/f/{name}</c>, with the
/// <see cref="MJCZone.DapperMatic.Security.DmAction.Execute"/> action: SQL the application writes, or a routine of the database. Nothing is
/// callable unless declared here; the SQL is the application's, and is never assembled from the request.
/// </summary>
/// <example>
/// <code>
/// options.Functions.Add(new DapperMaticFunction
/// {
///     Name = "top_customers",
///     Sql = "SELECT id, name, total FROM customer_totals WHERE tenant_id = @tenant ORDER BY total DESC LIMIT @count",
///     Parameters =
///     {
///         new DapperMaticFunctionParameter { Name = "count", Type = typeof(int), Default = 10 },
///         new DapperMaticFunctionParameter { Name = "tenant", Value = "@tenant.id" },
///     },
/// });
/// </code>
/// </example>
public sealed class DapperMaticFunction
{
    /// <summary>Gets or sets the name callers run it by, in the route and in policies' <c>Functions</c>.</summary>
    public required string Name { get; set; }

    /// <summary>Gets or sets the datasource it runs in, or null for every datasource.</summary>
    public string? DatasourceId { get; set; }

    /// <summary>Gets or sets a description, for the caller's OpenAPI document.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the SQL to run, its parameters written <c>@name</c>; or null to run <see cref="Routine"/>.
    /// </summary>
    public string? Sql { get; set; }

    /// <summary>
    /// Gets or sets a routine of the database to run with the parameters, in their order, as written (quoted or
    /// schema-qualified as the database needs): a PostgreSQL function (<c>SELECT * FROM routine(...)</c>), a SQL Server
    /// stored procedure (<c>EXEC routine @name = ...</c>) or a MySQL procedure (<c>CALL routine(...)</c>). SQLite has none.
    /// </summary>
    public string? Routine { get; set; }

    /// <summary>Gets the parameters, in order: those the caller gives, and those taken from the request's context.</summary>
    public List<DapperMaticFunctionParameter> Parameters { get; } = [];

    /// <summary>
    /// Gets or sets the tables it writes to, whose cached query results its runs make stale (<c>table</c> or
    /// <c>schema.table</c>); <c>*</c> for every table of the datasource.
    /// </summary>
    public string[] Writes { get; set; } = [];

    /// <summary>Gets or sets the most rows it may return, or null for <see cref="DapperMaticQueryLimits.MaxTop"/>.</summary>
    public int? MaxRows { get; set; }
}

/// <summary>
/// A parameter of a <see cref="DapperMaticFunction"/>.
/// </summary>
public sealed class DapperMaticFunctionParameter
{
    /// <summary>Gets or sets the name: <c>@name</c> in the SQL, and the argument's name in the request.</summary>
    public required string Name { get; set; }

    /// <summary>Gets or sets the type the argument is read as.</summary>
    public Type Type { get; set; } = typeof(string);

    /// <summary>
    /// Gets or sets a value of the request's context the parameter always takes, such as <c>@tenant.id</c> or
    /// <c>@user.id</c>; a request giving it is refused. Null for an argument the caller gives.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>Gets or sets the value when the caller gives none; a parameter with none and not nullable is required.</summary>
    public object? Default { get; set; }

    /// <summary>Gets or sets a value indicating whether the caller may give null, or leave it out with no default.</summary>
    public bool Nullable { get; set; }
}
