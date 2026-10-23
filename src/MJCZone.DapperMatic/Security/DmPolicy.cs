// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

#pragma warning disable SA1402 // The policy model belongs together.
#pragma warning disable SA1649

using System.Text.Json.Serialization;

namespace MJCZone.DapperMatic.Security;

/// <summary>
/// What a policy lets callers do. Written in JSON by name: <c>"Read, Create"</c>.
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter<DmAction>))]
public enum DmAction
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary>Read rows.</summary>
    Read = 1,

    /// <summary>Insert rows.</summary>
    Create = 2,

    /// <summary>Update rows.</summary>
    Update = 4,

    /// <summary>Delete rows.</summary>
    Delete = 8,

    /// <summary>Read definitions: schemas, tables, columns, indexes, constraints, views, data types, datasources.</summary>
    ReadSchema = 16,

    /// <summary>Create, change and drop schemas, tables, columns, indexes, constraints and views.</summary>
    ChangeSchema = 32,

    /// <summary>Add, change, remove and test datasources.</summary>
    ManageDatasources = 64,

    /// <summary>Read and change the policies and tenants kept by DapperMatic.</summary>
    ManageAccess = 128,

    /// <summary>
    /// Write a view's SQL, creating a view or changing its definition (with <see cref="ChangeSchema"/> on the view).
    /// The SQL can read any table the connection can, so this is for callers trusted to read everything in the
    /// datasource; a grant confined to some schemas or tables does not cover it.
    /// </summary>
    DefineViews = 256,

    /// <summary>
    /// Run a function the application declares. Its SQL reads and writes what it likes, so a grant covers it only when
    /// it names the function (<see cref="DmPolicy.Functions"/>), or names none and is confined to no schema or table.
    /// </summary>
    Execute = 512,

    /// <summary>Insert, update and delete rows.</summary>
    Write = Create | Update | Delete,

    /// <summary>Read and write rows.</summary>
    ReadWrite = Read | Write,

    /// <summary>Everything.</summary>
    All = ReadWrite | ReadSchema | ChangeSchema | ManageDatasources | ManageAccess | DefineViews | Execute,
}

/// <summary>
/// How a policy combines with others, as in PostgreSQL: permissive policies grant, and any one of them is enough;
/// restrictive policies fence, and every one of them must hold.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DmPolicyKind>))]
public enum DmPolicyKind
{
    /// <summary>Grants: the caller may act if any permissive policy matches; their filters are ORed.</summary>
    Permissive,

    /// <summary>Fences: every matching restrictive policy's filter and check must hold too; they are ANDed.</summary>
    Restrictive,
}

/// <summary>
/// Who may do what, to which tables, rows and columns. Nothing is allowed unless a permissive policy grants it.
/// </summary>
/// <remarks>
/// <para>
/// Targets: <see cref="Datasources"/>, <see cref="Schemas"/> and <see cref="Tables"/> are names or patterns with
/// <c>*</c>; empty means any. <c>_</c> in <see cref="Schemas"/> is the default schema of a request that names none.
/// <see cref="WithColumns"/> narrows row actions to tables having all those columns: one policy covers every table
/// with a <c>tenant_id</c>, however many schemas they are in.
/// </para>
/// <para>
/// Who: <see cref="Roles"/> (<c>anonymous</c>, <c>authenticated</c>, <c>*</c> for every caller, or the caller's
/// roles), required <see cref="Claims"/>, and a <see cref="Predicate"/> in code. A permissive policy must name roles, claims
/// or a predicate; a restrictive one with none applies to every caller.
/// </para>
/// <para>
/// Rows: <see cref="Filter"/> is a condition in the query syntax (<c>tenant_id eq null or tenant_id in
/// @tenant.below</c>) over the table's own columns and values of the request's context; it limits the rows read,
/// updated and deleted. <see cref="Check"/> is one every row written must satisfy, the filter when not given (a column
/// an insert does not give reads as null in it). <see cref="Presets"/> are written by the policy whatever the request
/// says, such as <c>tenant_id</c> = <c>@tenant.id</c>.
/// </para>
/// </remarks>
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class DmPolicy
{
    /// <summary>Gets or sets the policy's name, shown in effective permissions.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets whether the policy grants or fences.</summary>
    public DmPolicyKind Kind { get; set; } = DmPolicyKind.Permissive;

    /// <summary>Gets or sets what the policy lets callers do (or, restrictive, what it fences).</summary>
    public DmAction Actions { get; set; }

    /// <summary>Gets the roles the policy is for: <c>anonymous</c>, <c>authenticated</c>, <c>*</c>, or role names.</summary>
    public List<string> Roles { get; } = [];

    /// <summary>Gets claims the caller must have, by type: a value, or <c>*</c> for any.</summary>
    public Dictionary<string, string> Claims { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets a predicate in code the request's context must satisfy.</summary>
    [JsonIgnore]
    public Func<DmAccessContext, bool>? Predicate { get; set; }

    /// <summary>Gets the datasources the policy covers, by id or pattern; empty for all.</summary>
    public List<string> Datasources { get; } = [];

    /// <summary>Gets the schemas the policy covers, by name or pattern; <c>_</c> for the default one; empty for all.</summary>
    public List<string> Schemas { get; } = [];

    /// <summary>Gets the schemas the policy leaves out, by name or pattern.</summary>
    public List<string> ExceptSchemas { get; } = [];

    /// <summary>Gets the tables and views the policy covers, by name or pattern; empty for all.</summary>
    public List<string> Tables { get; } = [];

    /// <summary>Gets the tables and views the policy leaves out, by name or pattern.</summary>
    public List<string> ExceptTables { get; } = [];

    /// <summary>
    /// Gets the functions the policy lets callers run (<see cref="DmAction.Execute"/>), by name or pattern; empty for
    /// every one, when the policy is confined to no schema or table.
    /// </summary>
    public List<string> Functions { get; } = [];

    /// <summary>Gets columns a table must have for the policy's row actions to cover it.</summary>
    public List<string> WithColumns { get; } = [];

    /// <summary>Gets the columns the policy lets callers read and write; empty for all.</summary>
    public List<string> Columns { get; } = [];

    /// <summary>Gets the columns the policy keeps from callers.</summary>
    public List<string> ExcludeColumns { get; } = [];

    /// <summary>Gets or sets the rows the policy covers for reads, updates and deletes; null for all.</summary>
    public string? Filter { get; set; }

    /// <summary>
    /// Gets or sets the condition every row inserted or updated must satisfy. Null or blank, as in PostgreSQL, is the
    /// <see cref="Filter"/>: a row written must be one the policy covers, so a write cannot move a row out of a
    /// tenant's reach. A policy that lets callers write rows its filter does not cover says so with a check of its
    /// own, such as <c>true</c>.
    /// </summary>
    public string? Check { get; set; }

    /// <summary>Gets values the policy writes on insert and update, by column: a path such as <c>@tenant.id</c>, or a fixed text.</summary>
    public Dictionary<string, string?> Presets { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets where the policy came from: code, configuration, or the policies kept by DapperMatic.</summary>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the tenant a policy belongs to, as one a tenant's administrator keeps: it applies only to requests
    /// in that tenant, and only to that tenant's rows (or datasource or schema). Null for a policy of the whole
    /// application.
    /// </summary>
    public string? Tenant { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a grant of <see cref="DmAction.ManageAccess"/> is for the caller's own tenant only: its
    /// holders keep policies that belong to their tenant, giving no more than they may do themselves, and nothing else.
    /// </summary>
    public bool WithinOwnTenant { get; set; }

    /// <summary>
    /// Creates a policy granting actions to roles: <c>anonymous</c>, <c>authenticated</c>, <c>*</c> for every caller,
    /// or role names. Narrow it with the methods of <see cref="DmPolicyExtensions"/>.
    /// </summary>
    /// <param name="actions">What it grants.</param>
    /// <param name="roles">Who it grants it to; none, to grant by claims or a predicate instead.</param>
    /// <returns>The policy.</returns>
    public static DmPolicy Allow(DmAction actions, params string[] roles)
    {
        var policy = new DmPolicy { Actions = actions };
        policy.Roles.AddRange(roles ?? []);
        return policy;
    }

    /// <summary>
    /// Creates a policy fencing actions for roles, or for every caller when none is given: its filter, check and
    /// columns hold whatever the grants allow.
    /// </summary>
    /// <param name="actions">What it fences.</param>
    /// <param name="roles">Who it fences; none for every caller.</param>
    /// <returns>The policy.</returns>
    public static DmPolicy Restrict(DmAction actions, params string[] roles)
    {
        var policy = Allow(actions, roles);
        policy.Kind = DmPolicyKind.Restrictive;
        return policy;
    }

    /// <summary>
    /// Copies the policy, with lists of its own.
    /// </summary>
    /// <returns>The copy.</returns>
    public DmPolicy Copy()
    {
        var copy = new DmPolicy
        {
            Name = Name,
            Kind = Kind,
            Actions = Actions,
            Predicate = Predicate,
            Filter = Filter,
            Check = Check,
            Source = Source,
            Tenant = Tenant,
            WithinOwnTenant = WithinOwnTenant,
        };
        copy.Roles.AddRange(Roles);
        copy.Datasources.AddRange(Datasources);
        copy.Schemas.AddRange(Schemas);
        copy.ExceptSchemas.AddRange(ExceptSchemas);
        copy.Tables.AddRange(Tables);
        copy.ExceptTables.AddRange(ExceptTables);
        copy.Functions.AddRange(Functions);
        copy.WithColumns.AddRange(WithColumns);
        copy.Columns.AddRange(Columns);
        copy.ExcludeColumns.AddRange(ExcludeColumns);
        foreach (var (type, value) in Claims)
        {
            copy.Claims[type] = value;
        }

        foreach (var (column, value) in Presets)
        {
            copy.Presets[column] = value;
        }

        return copy;
    }

    /// <inheritdoc />
    public override string ToString() => Name ?? $"{Kind} {Actions} for {string.Join(", ", Roles)}";
}

/// <summary>
/// What a request acts on: a datasource, a schema, a table or view, and the table's columns when they are known.
/// </summary>
/// <param name="DatasourceId">The datasource; <see cref="Any"/> for every one, as listing them is.</param>
/// <param name="SchemaName">The schema, or null for the default one; <see cref="Any"/> for every one.</param>
/// <param name="TableName">The table or view, or null for a datasource or schema as a whole.</param>
/// <param name="Columns">The table's columns, which <see cref="DmPolicy.WithColumns"/> needs; null when not known.</param>
#pragma warning disable SA1313 // Positional record parameters are properties.
public sealed record DmResource(string? DatasourceId, string? SchemaName, string? TableName, IReadOnlyCollection<string>? Columns = null)
#pragma warning restore SA1313
{
    /// <summary>Every datasource, or every schema: a policy for any of them covers it.</summary>
    public const string Any = "*";

    /// <summary>Gets the function run, for <see cref="DmAction.Execute"/>; null for anything else.</summary>
    public string? FunctionName { get; init; }
}

/// <summary>
/// What the policies allow one caller to do to one resource.
/// </summary>
public sealed class DmAccessRules
{
    /// <summary>Gets a value indicating whether a permissive policy grants the action and no fence forbids it.</summary>
    public bool Allowed { get; init; }

    /// <summary>Gets a value indicating whether any policy covers the resource and action, for any caller.</summary>
    public bool Targeted { get; init; }

    /// <summary>Gets the permissive policies that grant the action to the caller.</summary>
    public IReadOnlyList<DmPolicy> Permissive { get; init; } = [];

    /// <summary>Gets the restrictive policies that apply to the caller.</summary>
    public IReadOnlyList<DmPolicy> Restrictive { get; init; } = [];

    /// <summary>
    /// Gets the columns the caller may read or write, or null for all: the permissive policies' columns together,
    /// less what any applying policy excludes.
    /// </summary>
    public IReadOnlySet<string>? Columns { get; init; }

    /// <summary>Gets the values the policies write, by column; a restrictive policy's win over a permissive one's.</summary>
    public IReadOnlyDictionary<string, string?> Presets { get; init; } = new Dictionary<string, string?>();

    /// <summary>
    /// Gets the condition limiting the rows, in the query syntax: the permissive filters ORed (none if any of them has
    /// none), ANDed with every restrictive filter. Null for every row.
    /// </summary>
    public string? Filter => Combine(Permissive.Select(p => p.Filter), Restrictive.Select(r => r.Filter));

    /// <summary>
    /// Gets the condition every row written must satisfy, combined as <see cref="Filter"/> is, each policy's check
    /// being its filter when it has none; null for none.
    /// </summary>
    public string? Check => Combine(Permissive.Select(CheckOf), Restrictive.Select(CheckOf));

    /// <summary>Gets rules that allow nothing.</summary>
    internal static DmAccessRules Denied { get; } = new();

    /// <summary>Gets rules that allow everything, with no conditions: for a resource no policy covers, where code decides.</summary>
    internal static DmAccessRules Unrestricted { get; } = new() { Allowed = true };

    // A blank check is no check given, as an absent one is: the filter. To check nothing, a policy's check is "true".
    private static string? CheckOf(DmPolicy policy) => string.IsNullOrWhiteSpace(policy.Check) ? policy.Filter : policy.Check;

    private static string? Combine(IEnumerable<string?> permissive, IEnumerable<string?> restrictive)
    {
        var granted = permissive.ToList();
        var parts = new List<string>();
        if (granted.Count > 0 && granted.All(f => !string.IsNullOrWhiteSpace(f)))
        {
            parts.Add(granted.Count == 1 ? $"({granted[0]})" : $"({string.Join(" or ", granted.Select(f => $"({f})"))})");
        }

        parts.AddRange(restrictive.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => $"({f})"));
        return parts.Count == 0 ? null : string.Join(" and ", parts);
    }
}
