// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using MJCZone.DapperMatic.Dml.Rest;

namespace MJCZone.DapperMatic.Security;

/// <summary>
/// Evaluates policies, PostgreSQL-style: an action is allowed when a permissive policy grants it; the permissive
/// policies' filters are ORed and every restrictive policy's filter is ANDed on top.
/// </summary>
public static class DmPolicies
{
    private static readonly ConcurrentDictionary<string, Regex> Patterns = new(StringComparer.Ordinal);

    /// <summary>
    /// Works out what the policies let a caller do to a resource.
    /// </summary>
    /// <param name="policies">The policies.</param>
    /// <param name="context">The caller, and the request's tenant and values.</param>
    /// <param name="resource">What the request acts on.</param>
    /// <param name="action">The action, one of <see cref="DmAction"/>'s single values.</param>
    /// <returns>The rules: whether it is allowed, and the filter, check, columns and presets that apply.</returns>
    public static DmAccessRules Evaluate(IEnumerable<DmPolicy> policies, DmAccessContext context, DmResource resource, DmAction action)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);
        return Limit(Rules(policies, context, resource, action), context, resource, action);
    }

    /// <summary>
    /// Holds rules to the most the caller may do (<see cref="DmAccessContext.Limits"/>, an API key's): allowed only if
    /// the limits allow it too; the limits' filters and checks ANDed in, their columns intersected. Limits only take
    /// away: their presets are not read, and access management (which keeps policies for every datasource and table)
    /// is allowed only by a limit confined to none. Rules of no limits are as they are. Where code decided (rules that
    /// allow everything), the limits still hold.
    /// </summary>
    /// <param name="rules">What the caller's policies (or code) allow.</param>
    /// <param name="context">The caller.</param>
    /// <param name="resource">What the request acts on.</param>
    /// <param name="action">The action.</param>
    /// <returns>The rules within the limits.</returns>
    public static DmAccessRules Limit(DmAccessRules rules, DmAccessContext context, DmResource resource, DmAction action)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);
        if (context.Limits is not { } limits || !rules.Allowed)
        {
            return rules;
        }

        var within = Rules(limits, context, resource, action);
        if (!within.Allowed
            || ((action & DmAction.ManageAccess) != 0
                && !within.Permissive.Any(g => g.Datasources.Count == 0 && g.Schemas.Count == 0 && g.ExceptSchemas.Count == 0 && g.Tables.Count == 0 && g.ExceptTables.Count == 0)))
        {
            return new DmAccessRules { Targeted = true };
        }

        IReadOnlySet<string>? columns = rules.Columns is null ? within.Columns
            : within.Columns is null ? rules.Columns
            : new HashSet<string>(rules.Columns.Where(within.Columns.Contains), StringComparer.OrdinalIgnoreCase);
        // The limits' own conditions, one fence more: ANDed with whatever the policies give.
        var fence = new DmPolicy { Name = "limits", Kind = DmPolicyKind.Restrictive, Filter = within.Filter, Check = within.Check };
        return new DmAccessRules
        {
            Allowed = true,
            Targeted = true,
            Permissive = rules.Permissive,
            Restrictive = [.. rules.Restrictive, fence],
            Columns = columns,
            Presets = rules.Presets,
        };
    }

    /// <summary>
    /// Refuses a policy that cannot work: a grant for nobody in particular, no actions, or a filter or check that is
    /// malformed or reads more than the table's own columns and the request's values.
    /// </summary>
    /// <param name="policy">The policy.</param>
    /// <exception cref="ArgumentException">The policy cannot work; the message says why.</exception>
    public static void Validate(DmPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var name = policy.Name ?? policy.ToString();
        if (policy.Actions == DmAction.None)
        {
            throw new ArgumentException($"The policy '{name}' allows no actions.");
        }

        if (policy.Kind == DmPolicyKind.Permissive && policy.Roles.Count == 0 && policy.Claims.Count == 0 && policy.Predicate is null)
        {
            throw new ArgumentException($"The policy '{name}' grants to nobody in particular: name its roles (\"*\" for every caller), claims, or a predicate.");
        }

        if (policy.Functions.Count > 0 && policy.Actions != DmAction.Execute)
        {
            throw new ArgumentException($"The policy '{name}' names functions, so it grants Execute and nothing else: grant its other actions in a policy of their own.");
        }

        foreach (var (what, text) in new[] { ("filter", policy.Filter), ("check", policy.Check) })
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                RequireOwnColumns(RestParser.ParseFilter(text), name, what);
            }
        }
    }

    /// <summary>
    /// Tells whether a policy reaches only datasources and schemas that grants of <see cref="DmAction.ManageAccess"/>
    /// cover: an administrator granted access management for some datasources or schemas may keep policies for those,
    /// and no others. A grant naming no datasource and no schema covers everything.
    /// </summary>
    /// <param name="managing">The caller's rules for <see cref="DmAction.ManageAccess"/>, evaluated for every datasource and schema.</param>
    /// <param name="datasources">The datasources the policy or tenant reaches, by id; empty for every one.</param>
    /// <param name="schemas">The schemas it reaches, by name; empty for every one.</param>
    /// <returns>True if the grants cover all it reaches.</returns>
    public static bool IsWithin(DmAccessRules managing, IReadOnlyCollection<string> datasources, IReadOnlyCollection<string> schemas)
    {
        ArgumentNullException.ThrowIfNull(managing);
        ArgumentNullException.ThrowIfNull(datasources);
        ArgumentNullException.ThrowIfNull(schemas);
        if (!managing.Allowed)
        {
            return false;
        }

        if (managing.Permissive.Count == 0 || managing.Permissive.Any(g => g.Datasources.Count == 0 && g.Schemas.Count == 0 && g.ExceptSchemas.Count == 0))
        {
            // Allowed with no grant is code's decision (custom permissions); a grant naming nothing covers everything.
            return true;
        }

        // A pattern reaches datasources or schemas no grant can be shown to cover.
        if (datasources.Count == 0 || datasources.Any(IsPattern) || schemas.Any(IsPattern))
        {
            return false;
        }

        return datasources.All(d =>
            schemas.Count == 0
                ? managing.Permissive.Any(g => Any(g.Datasources, d) && g.Schemas.Count == 0 && g.ExceptSchemas.Count == 0)
                : schemas.All(s =>
                    managing.Permissive.Any(g =>
                        Any(g.Datasources, d) && Any(g.Schemas, s) && !(g.ExceptSchemas.Count > 0 && Matches(g.ExceptSchemas, s))
                    )
                )
        );
    }

    // Whether a policy is about this resource and action, for anyone.
    private static bool Covers(DmPolicy policy, DmResource resource, DmAction action)
    {
        // "*" is every datasource or schema, as listing them is: the policy's patterns do not narrow it, and the list
        // is narrowed item by item.
        if (resource.DatasourceId != DmResource.Any && !Any(policy.Datasources, resource.DatasourceId))
        {
            return false;
        }

        // A function's SQL reads and writes what it likes: only a policy naming it, or one naming no function that is
        // confined to no schema or table, covers running it; and a policy naming functions covers nothing else.
        if (action == DmAction.Execute)
        {
            return resource.FunctionName is { } function && (policy.Functions.Count > 0 ? Matches(policy.Functions, function) : !Confined(policy));
        }

        if (policy.Functions.Count > 0)
        {
            return false;
        }

        // Managing a datasource, or writing a view's SQL, acts on all of it: a policy confined to some of its schemas
        // or tables does not cover that.
        if ((action & (DmAction.ManageDatasources | DmAction.DefineViews)) != 0 && Confined(policy))
        {
            return false;
        }

        var schema = resource.SchemaName ?? "_";
        if (schema != DmResource.Any
            && (!Any(policy.Schemas, schema) || (policy.ExceptSchemas.Count > 0 && Matches(policy.ExceptSchemas, schema))))
        {
            return false;
        }

        // A datasource or schema as a whole: the policy's table names do not narrow it.
        if (resource.TableName is { } table && (!Any(policy.Tables, table) || (policy.ExceptTables.Count > 0 && Matches(policy.ExceptTables, table))))
        {
            return false;
        }

        if (policy.WithColumns.Count > 0)
        {
            var rows = (action & DmAction.ReadWrite) != 0;
            return rows
                && resource.Columns is { } columns
                && policy.WithColumns.All(c => columns.Contains(c, StringComparer.OrdinalIgnoreCase));
        }

        return true;
    }

    // Whether a policy is for this caller.
    // What the policies let a caller do to a resource, before any limits.
    private static DmAccessRules Rules(IEnumerable<DmPolicy> policies, DmAccessContext context, DmResource resource, DmAction action)
    {
        // A policy of a tenant's is about that tenant's requests only: for any other, it is not there at all.
        var tenant = context.Tenant?.Id.ToString();
        var covering = policies
            .Where(p => (p.Actions & action) == action && action != DmAction.None && (p.Tenant is null || p.Tenant == tenant) && Covers(p, resource, action))
            .ToList();
        if (covering.Count == 0)
        {
            return DmAccessRules.Denied;
        }

        var applying = covering.Where(p => Applies(p, context)).ToList();
        var permissive = applying.Where(p => p.Kind == DmPolicyKind.Permissive).ToList();
        var restrictive = applying.Where(p => p.Kind == DmPolicyKind.Restrictive).ToList();
        if (permissive.Count == 0)
        {
            return new DmAccessRules { Targeted = true, Restrictive = restrictive };
        }

        // Columns: what any grant allows, less what any applying policy excludes, within every fence's own list.
        HashSet<string>? columns = permissive.Any(p => p.Columns.Count == 0)
            ? null
            : new HashSet<string>(permissive.SelectMany(p => p.Columns), StringComparer.OrdinalIgnoreCase);
        foreach (var fence in restrictive.Where(r => r.Columns.Count > 0))
        {
            columns = columns is null
                ? new HashSet<string>(fence.Columns, StringComparer.OrdinalIgnoreCase)
                : [.. columns.Where(c => fence.Columns.Contains(c, StringComparer.OrdinalIgnoreCase))];
        }

        var excluded = applying.SelectMany(p => p.ExcludeColumns).ToList();
        if (excluded.Count > 0)
        {
            columns = columns is null
                ? (resource.Columns is { } all ? new HashSet<string>(all, StringComparer.OrdinalIgnoreCase) : null)
                : columns;
            columns?.ExceptWith(excluded);
        }

        var presets = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var policy in permissive.Concat(restrictive))
        {
            foreach (var (column, value) in policy.Presets)
            {
                presets[column] = value;
            }
        }

        return new DmAccessRules
        {
            Allowed = true,
            Targeted = true,
            Permissive = permissive,
            Restrictive = restrictive,
            Columns = columns,
            Presets = presets,
        };
    }

    private static bool Applies(DmPolicy policy, DmAccessContext context)
    {
        if (policy.Roles.Count > 0 && !policy.Roles.Any(r => r == "*" || context.IsInRole(r)))
        {
            return false;
        }

        foreach (var (type, value) in policy.Claims)
        {
            var claims = context.User.FindAll(type);
            if (!claims.Any(c => value == "*" || string.Equals(c.Value, value, StringComparison.Ordinal)))
            {
                return false;
            }
        }

        return policy.Predicate?.Invoke(context) ?? true;
    }

    private static bool Confined(DmPolicy policy) =>
        policy.ExceptSchemas.Count > 0 || (policy.Schemas.Count > 0 && !policy.Schemas.Contains("*"))
        || policy.ExceptTables.Count > 0 || (policy.Tables.Count > 0 && !policy.Tables.Contains("*"))
        || policy.WithColumns.Count > 0;

    private static bool IsPattern(string name) => name.Contains('*', StringComparison.Ordinal) || name.Contains('?', StringComparison.Ordinal);

    private static bool Any(List<string> patterns, string? value) => patterns.Count == 0 || (value is not null && Matches(patterns, value));

    private static bool Matches(List<string> patterns, string value) =>
        patterns.Any(p => Patterns.GetOrAdd(p, Compile).IsMatch(value));

    private static Regex Compile(string pattern) =>
        new(
            "^" + Regex.Escape(pattern).Replace(@"\*", ".*", StringComparison.Ordinal).Replace(@"\?", ".", StringComparison.Ordinal) + "$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

    // A policy's filter and check read the table's own columns and the request's values: no relations, no lambdas.
    private static void RequireOwnColumns(RestNode node, string name, string what)
    {
        var pending = new Stack<RestNode>([node]);
        while (pending.TryPop(out var current))
        {
            switch (current)
            {
                case RestPath { Segments.Count: > 1 } path:
                    throw new ArgumentException(
                        $"The policy '{name}' {what} reads {path}, another table's column: a policy reads its table's own columns and values such as @tenant.id."
                    );
                case RestLambda lambda:
                    throw new ArgumentException($"The policy '{name}' {what} tests related rows ({lambda.Collection}): a policy reads its table's own columns.");
                case RestBinary binary:
                    pending.Push(binary.Left);
                    pending.Push(binary.Right);
                    break;
                case RestNot not:
                    pending.Push(not.Operand);
                    break;
                case RestIn @in:
                    pending.Push(@in.Operand);
                    break;
                case RestCall call:
                    call.Arguments.ToList().ForEach(pending.Push);
                    break;
            }
        }
    }
}
