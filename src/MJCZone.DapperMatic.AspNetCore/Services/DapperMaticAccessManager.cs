// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Auditing;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Validation;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Reads and changes the policies and tenants DapperMatic keeps, for its access endpoints. Every change needs
/// <see cref="DmAction.ManageAccess"/>; a caller granted it for some datasources or schemas reads and changes only
/// policies and tenants confined to those, so a tenant's administrator cannot reach beyond the tenant. Policies and
/// tenants of code and configuration are shown, and cannot be changed here.
/// </summary>
public sealed partial class DapperMaticAccessManager
{
    private static readonly DmAction[] SingleActions =
    [
        DmAction.Read,
        DmAction.Create,
        DmAction.Update,
        DmAction.Delete,
        DmAction.ReadSchema,
        DmAction.ChangeSchema,
        DmAction.ManageDatasources,
        DmAction.ManageAccess,
        DmAction.DefineViews,
    ];

    private readonly DapperMaticPolicySet _policies;
    private readonly DapperMaticStoredAccess _stored;
    private readonly IDapperMaticPermissions _permissions;
    private readonly IDapperMaticAuditLogger _auditLogger;
    private readonly IDapperMaticService _service;
    private readonly DapperMaticOptions _options;
    private readonly Changes.DapperMaticChangeLog? _changes;
    private readonly IDapperMaticDatasourceRepository? _datasources;
    private readonly Factories.IDbConnectionFactory? _connections;
    private readonly Authentication.DapperMaticApiKeyService? _keys;
    private readonly IServiceProvider? _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticAccessManager"/> class.
    /// </summary>
    /// <param name="policies">The policies in force.</param>
    /// <param name="stored">The policies and tenants kept by DapperMatic.</param>
    /// <param name="permissions">The permissions every operation is authorized by.</param>
    /// <param name="auditLogger">The audit logger.</param>
    /// <param name="service">The service, for a table's columns.</param>
    /// <param name="options">The options, with the tenants of configuration.</param>
    /// <param name="changes">Where webhooks' changes are noted, for their status; or null.</param>
    /// <param name="datasources">The datasources, for webhooks' status; or null.</param>
    /// <param name="connections">Makes connections to them; or null.</param>
    /// <param name="services">The request's services, for the API keys once they are turned on; or null.</param>
    public DapperMaticAccessManager(
        DapperMaticPolicySet policies,
        DapperMaticStoredAccess stored,
        IDapperMaticPermissions permissions,
        IDapperMaticAuditLogger auditLogger,
        IDapperMaticService service,
        IOptions<DapperMaticOptions> options,
        Changes.DapperMaticChangeLog? changes = null,
        IDapperMaticDatasourceRepository? datasources = null,
        Factories.IDbConnectionFactory? connections = null,
        IServiceProvider? services = null
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        _policies = policies;
        _stored = stored;
        _permissions = permissions;
        _auditLogger = auditLogger;
        _service = service;
        _options = options.Value;
        _changes = changes;
        _datasources = datasources;
        _connections = connections;
        _keys = services?.GetService(typeof(Authentication.DapperMaticApiKeyService)) as Authentication.DapperMaticApiKeyService;
        _services = services;
    }

    /// <summary>
    /// Lists the policies in force that the caller may manage: of code and configuration (their <c>Source</c> says so),
    /// and kept by DapperMatic. A tenant's administrator sees their tenant's; another administrator sees every one
    /// they manage, or one tenant's when <paramref name="tenant"/> names it.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="tenant">A tenant whose policies to list, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The policies.</returns>
    public async Task<IReadOnlyList<DmPolicy>> GetPoliciesAsync(DapperMaticOperation context, string? tenant = null, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(context, cancellationToken).ConfigureAwait(false);
        var (managing, own) = Scope(context, tenant);
        return own is not null
            ? [.. _stored.Policies.Where(p => p.Tenant == own)]
            : [.. Kept().Where(p => (tenant is null || p.Tenant == tenant) && Within(managing, p))];
    }

    /// <summary>
    /// Gets a policy in force by its name: the application's, or, with <paramref name="tenant"/>, a tenant's (a tenant's
    /// administrator always gets their tenant's).
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="name">The policy's name.</param>
    /// <param name="tenant">The tenant the policy is one of, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The policy.</returns>
    /// <exception cref="KeyNotFoundException">No policy the caller may manage has the name.</exception>
    public async Task<DmPolicy> GetPolicyAsync(DapperMaticOperation context, string name, string? tenant = null, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(context, cancellationToken).ConfigureAwait(false);
        var (managing, own) = Scope(context, tenant);
        var key = own ?? tenant;
        return Kept().FirstOrDefault(p => DapperMaticAccessEdits.Same(p, name, key)) is { } policy && (own is not null || Within(managing, policy))
            ? policy
            : throw new KeyNotFoundException($"No policy is named '{name}'.");
    }

    /// <summary>
    /// Adds a policy to those kept by DapperMatic, or replaces the kept one of the same name (and tenant). It applies
    /// to the next request. A tenant's administrator keeps their tenant's policies only: each is held to the tenant's
    /// rows (or datasource or schema), names its tables, and gives no more than the administrator may do.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="name">The policy's name.</param>
    /// <param name="policy">The policy.</param>
    /// <param name="tenant">The tenant the policy is one of, or null for the application's.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The policy kept.</returns>
    /// <exception cref="ArgumentException">The policy cannot work; the message says why.</exception>
    /// <exception cref="DuplicateKeyException">A policy of code or configuration has the name.</exception>
    /// <exception cref="UnauthorizedAccessException">The policy, or the one it replaces, reaches beyond what the caller manages.</exception>
    public async Task<DmPolicy> SavePolicyAsync(
        DapperMaticOperation context,
        string name,
        DmPolicy policy,
        string? tenant = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(policy);
        await AuthorizeChangeAsync(context, cancellationToken).ConfigureAwait(false);
        var (managing, own) = Scope(context, tenant);
        policy.Name = name;
        policy.Source = null;
        policy.Predicate = null;
        policy.Tenant = own ?? tenant;
        DmPolicies.Validate(policy);
        if (policy.Tenant is null)
        {
            if (Find(_policies.Configured, name) is not null)
            {
                throw new DuplicateKeyException($"The policy '{name}' is given in code or configuration, and is changed there.");
            }

            if (!Within(managing, policy) || (Kept().FirstOrDefault(p => DapperMaticAccessEdits.Same(p, name, null)) is { } kept && !Within(managing, kept)))
            {
                throw new UnauthorizedAccessException(
                    $"The policy '{name}' reaches datasources or schemas this caller does not manage access to: name them, without patterns."
                );
            }
        }
        else
        {
            var settings = own is not null ? Access(context).Tenant : TenantOf(policy.Tenant);
            HoldToTenant(policy, settings);
            if (own is not null)
            {
                await AssertNoMoreThanOwnAsync(context, policy, cancellationToken).ConfigureAwait(false);
            }
            else if (!Within(managing, policy)
                || (_stored.Policies.FirstOrDefault(p => DapperMaticAccessEdits.Same(p, name, policy.Tenant)) is { } kept && !Within(managing, kept)))
            {
                throw new UnauthorizedAccessException($"The policy '{name}' reaches datasources or schemas this caller does not manage access to.");
            }
        }

        await _stored.Repository.SavePolicyAsync(policy, cancellationToken).ConfigureAwait(false);
        await ChangedAsync(context, $"Saved the policy '{name}'" + (policy.Tenant is null ? string.Empty : $" of the tenant '{policy.Tenant}'"), cancellationToken)
            .ConfigureAwait(false);
        return policy;
    }

    /// <summary>
    /// Removes a policy kept by DapperMatic: the application's, or, with <paramref name="tenant"/>, a tenant's (a
    /// tenant's administrator always removes their tenant's).
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="name">The policy's name.</param>
    /// <param name="tenant">The tenant the policy is one of, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    /// <exception cref="KeyNotFoundException">No kept policy the caller may manage has the name.</exception>
    /// <exception cref="DuplicateKeyException">The policy is given in code or configuration.</exception>
    public async Task DeletePolicyAsync(DapperMaticOperation context, string name, string? tenant = null, CancellationToken cancellationToken = default)
    {
        await AuthorizeChangeAsync(context, cancellationToken).ConfigureAwait(false);
        var (managing, own) = Scope(context, tenant);
        var key = own ?? tenant;
        if (key is null && Find(_policies.Configured, name) is not null)
        {
            throw new DuplicateKeyException($"The policy '{name}' is given in code or configuration, and is removed there.");
        }

        if (_stored.Policies.FirstOrDefault(p => DapperMaticAccessEdits.Same(p, name, key)) is not { } kept || (own is null && !Within(managing, kept)))
        {
            throw new KeyNotFoundException($"No policy is named '{name}'.");
        }

        await _stored.Repository.DeletePolicyAsync(name, key, cancellationToken).ConfigureAwait(false);
        await ChangedAsync(context, $"Removed the policy '{name}'" + (key is null ? string.Empty : $" of the tenant '{key}'"), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists the tenants the caller may manage: of configuration, and kept by DapperMatic (which win).
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tenants, by id.</returns>
    public async Task<IReadOnlyDictionary<string, DapperMaticTenantSettings>> GetTenantsAsync(DapperMaticOperation context, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(context, cancellationToken).ConfigureAwait(false);
        var managing = Managing(context);
        var tenants = new Dictionary<string, DapperMaticTenantSettings>(_options.Tenancy.Tenants, StringComparer.OrdinalIgnoreCase);
        foreach (var (id, tenant) in _stored.Tenants)
        {
            tenants[id] = tenant;
        }

        return tenants.Where(t => Within(managing, t.Value)).ToDictionary(t => t.Key, t => t.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets a tenant by its id.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="id">The tenant's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What is known of it.</returns>
    /// <exception cref="KeyNotFoundException">No tenant the caller may manage has the id.</exception>
    public async Task<DapperMaticTenantSettings> GetTenantAsync(DapperMaticOperation context, string id, CancellationToken cancellationToken = default) =>
        (await GetTenantsAsync(context, cancellationToken).ConfigureAwait(false)).GetValueOrDefault(id)
        ?? throw new KeyNotFoundException($"No tenant has the id '{id}'.");

    /// <summary>
    /// Adds a tenant to those kept by DapperMatic, or replaces the kept one of the same id. A tenant of configuration
    /// with the id is overridden.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="id">The tenant's id.</param>
    /// <param name="tenant">What is known of it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tenant kept.</returns>
    /// <exception cref="UnauthorizedAccessException">The tenant, or the one it replaces, is in a datasource or schema the caller does not manage access to.</exception>
    public async Task<DapperMaticTenantSettings> SaveTenantAsync(
        DapperMaticOperation context,
        string id,
        DapperMaticTenantSettings tenant,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(tenant);
        await AuthorizeChangeAsync(context, cancellationToken).ConfigureAwait(false);
        var managing = Managing(context);
        // A tenant kept, or of configuration, with the id is replaced or overridden: the caller must manage it too.
        if (!Within(managing, tenant)
            || (_stored.Tenants.GetValueOrDefault(id) is { } kept && !Within(managing, kept))
            || (_options.Tenancy.Tenants.TryGetValue(id, out var configured) && !Within(managing, configured)))
        {
            throw new UnauthorizedAccessException($"The tenant '{id}' is in a datasource or schema this caller does not manage access to.");
        }

        await _stored.Repository.SaveTenantAsync(id, tenant, cancellationToken).ConfigureAwait(false);
        await ChangedAsync(context, $"Saved the tenant '{id}'", cancellationToken).ConfigureAwait(false);
        return tenant;
    }

    /// <summary>
    /// Removes a tenant kept by DapperMatic.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="id">The tenant's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    /// <exception cref="KeyNotFoundException">No kept tenant the caller may manage has the id.</exception>
    public async Task DeleteTenantAsync(DapperMaticOperation context, string id, CancellationToken cancellationToken = default)
    {
        await AuthorizeChangeAsync(context, cancellationToken).ConfigureAwait(false);
        if (_stored.Tenants.GetValueOrDefault(id) is not { } kept || !Within(Managing(context), kept))
        {
            throw new KeyNotFoundException($"No tenant kept by DapperMatic has the id '{id}'.");
        }

        await _stored.Repository.DeleteTenantAsync(id, cancellationToken).ConfigureAwait(false);
        await ChangedAsync(context, $"Removed the tenant '{id}'", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Exports the policies and tenants kept by DapperMatic that the caller may manage.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The policies and tenants.</returns>
    public async Task<DapperMaticAccessDocument> ExportAsync(DapperMaticOperation context, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(context, cancellationToken).ConfigureAwait(false);
        var managing = Managing(context);
        var document = new DapperMaticAccessDocument();
        document.Policies.AddRange(_stored.Policies.Where(p => Within(managing, p)));
        foreach (var (id, tenant) in _stored.Tenants.Where(t => Within(managing, t.Value)))
        {
            document.Tenants[id] = tenant;
        }

        // Service accounts reach every datasource their roles do: exported to those who manage access to everything.
        // Their keys are never exported.
        if (DmPolicies.IsWithin(managing, [], []))
        {
            document.ServiceAccounts.AddRange(_stored.ServiceAccounts);
        }

        return document.Copy();
    }

    /// <summary>
    /// Imports policies and tenants: every one is checked before any is kept. Importing needs access management for
    /// every datasource and schema.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="document">The policies and tenants.</param>
    /// <param name="replace">Whether they replace everything kept; otherwise they are added, replacing those of the same names.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    /// <exception cref="ArgumentException">A policy cannot work, has no name, or shares its name; the message says which.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller manages access to only some datasources or schemas.</exception>
    public async Task ImportAsync(DapperMaticOperation context, DapperMaticAccessDocument document, bool replace, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        await AuthorizeChangeAsync(context, cancellationToken).ConfigureAwait(false);
        if (!DmPolicies.IsWithin(Managing(context), [], []))
        {
            throw new UnauthorizedAccessException("Importing needs access management for every datasource and schema.");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var policy in document.Policies)
        {
            if (string.IsNullOrWhiteSpace(policy.Name) || !names.Add(policy.Name))
            {
                throw new ArgumentException($"Every policy imported has a name of its own: '{policy.Name}' is missing or repeated.");
            }

            if (Find(_policies.Configured, policy.Name) is not null)
            {
                throw new ArgumentException($"The policy '{policy.Name}' is given in code or configuration.");
            }

            policy.Source = null;
            DmPolicies.Validate(policy);
        }

        // Webhooks, with their addresses and secrets, are kept through their own endpoints: an import leaves them as
        // they are.
        document.Webhooks.Clear();
        if (replace)
        {
            document.Webhooks.AddRange((await _stored.Repository.GetAsync(cancellationToken).ConfigureAwait(false)).Webhooks);
            await _stored.Repository.ReplaceAsync(document, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            foreach (var policy in document.Policies)
            {
                await _stored.Repository.SavePolicyAsync(policy, cancellationToken).ConfigureAwait(false);
            }

            foreach (var (id, tenant) in document.Tenants)
            {
                await _stored.Repository.SaveTenantAsync(id, tenant, cancellationToken).ConfigureAwait(false);
            }

            foreach (var account in document.ServiceAccounts)
            {
                await _stored.Repository.SaveServiceAccountAsync(account, cancellationToken).ConfigureAwait(false);
            }
        }

        await ChangedAsync(
                context,
                $"Imported {document.Policies.Count} policies and {document.Tenants.Count} tenants{(replace ? ", replacing those kept" : string.Empty)}",
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Works out what the policies let the caller do to a datasource, schema, table or view: for a user interface to
    /// show what is possible, or an administrator to see why something is refused. The conditions and the policies'
    /// names are shown only to a caller who manages access to the resource.
    /// </summary>
    /// <param name="context">The operation, naming the resource.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rules for each action.</returns>
    public async Task<DapperMaticEffectiveAccess> GetEffectiveAsync(DapperMaticOperation context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        await AuthorizeAsync(context, cancellationToken).ConfigureAwait(false);

        // The datasource and schema come from the query, after the request was held to its tenant's: held now.
        DapperMaticAccess.Hold(Access(context), context.DatasourceId, context.SchemaName);
        if (_service is DapperMaticService service)
        {
            context = await service.ResolveSchemaAsync(context).ConfigureAwait(false);
        }

        IReadOnlyCollection<string>? columns = null;
        if (context.DatasourceId is { Length: > 0 } datasourceId && context.TableName is { Length: > 0 } tableName)
        {
            var table = await _service
                .GetTableAsync(context, datasourceId, tableName, context.SchemaName, includeColumns: true, includeIndexes: false, includeConstraints: false, cancellationToken)
                .ConfigureAwait(false);
            columns = table.Columns?.Select(c => c.ColumnName).ToList();
        }

        var resource = DapperMaticPolicySet.ResourceOf(context) with { Columns = columns };
        var access = Access(context);
        var detailed = DmPolicies.IsWithin(
            Managing(context),
            context.DatasourceId is { Length: > 0 } datasource ? [datasource] : [],
            context.SchemaName is { Length: > 0 } schema ? [schema] : []
        );
        var effective = new DapperMaticEffectiveAccess { DatasourceId = context.DatasourceId, SchemaName = context.SchemaName, TableName = context.TableName };
        foreach (var action in SingleActions)
        {
            var rules = _policies.Evaluate(access, resource, action);
            effective.Actions[action.ToString()] = new DapperMaticEffectiveAction
            {
                Allowed = rules.Allowed,
                Columns = rules.Columns?.Order(StringComparer.OrdinalIgnoreCase).ToList(),
                Filter = detailed ? rules.Filter : null,
                Check = detailed && (action & DmAction.Write) != 0 ? rules.Check : null,
                Presets = detailed && rules.Presets.Count > 0 ? rules.Presets : null,
                Policies = detailed ? [.. rules.Permissive.Concat(rules.Restrictive).Select(p => p.ToString())] : null,
            };
        }

        return effective;
    }

    private static DmPolicy? Find(IEnumerable<DmPolicy> policies, string name) =>
        policies.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    // A grant to manage access within the caller's own tenant: marked so, or one of a tenant's own policies.
    private static bool OwnTenant(DmPolicy policy) => policy.WithinOwnTenant || policy.Tenant is not null;

    // A tenant's policy is held to the tenant: to its datasource and schema when it has them, and, where a column marks
    // a tenant's rows, to those (the policy set adds that condition per table, so it names its tables). It never
    // manages datasources or writes views' SQL, which reach a whole datasource; it changes schemas only where the tenant
    // has one of its own.
    private void HoldToTenant(DmPolicy policy, DmTenant? tenant)
    {
        var tenancy = _options.Tenancy;
        var marked = tenancy.RowsColumn is not null || tenancy.RowsColumns.Count > 0;
        var own = tenant?.DatasourceId is not null || tenant?.SchemaName is not null;
        if (!marked && !own)
        {
            throw new UnauthorizedAccessException(
                $"A policy of the tenant '{policy.Tenant}' cannot be held to it: name the column marking a tenant's rows (Tenancy.RowsMarkedBy), or give the tenant a datasource or schema."
            );
        }

        if ((policy.Actions & (DmAction.ManageDatasources | DmAction.DefineViews)) != 0 || ((policy.Actions & DmAction.ChangeSchema) != 0 && !own))
        {
            throw new UnauthorizedAccessException("A tenant's policy cannot manage datasources, write views' SQL, or change schemas the tenant shares with others.");
        }

        var onTables = (policy.Actions & ~DmAction.ManageAccess) != DmAction.None;
        if (marked && onTables && (policy.Tables.Count == 0 || policy.Tables.Any(IsPattern) || policy.ExceptTables.Count > 0))
        {
            throw new ArgumentException("A tenant's policy names its tables, without patterns, so that each is held to the tenant's rows.");
        }

        if (tenant?.DatasourceId is { } datasource)
        {
            policy.Datasources.Clear();
            policy.Datasources.Add(datasource);
        }

        if (tenant?.SchemaName is { } schema)
        {
            policy.Schemas.Clear();
            policy.Schemas.Add(schema);
            policy.ExceptSchemas.Clear();
        }

        if ((policy.Actions & DmAction.ManageAccess) != 0)
        {
            policy.WithinOwnTenant = true;
        }
    }

    // A tenant's administrator gives no more than they may do: each action, on each table the policy names, with no
    // column they may not use; and a grant only to callers who sign in.
    private async Task AssertNoMoreThanOwnAsync(DapperMaticOperation context, DmPolicy policy, CancellationToken cancellationToken)
    {
        if (policy.Kind == DmPolicyKind.Restrictive)
        {
            return;
        }

        if (policy.Roles.Any(r => r is "*" or "anonymous"))
        {
            throw new UnauthorizedAccessException("A tenant's policy grants to callers who sign in: name roles, or \"authenticated\".");
        }

        var access = Access(context);
        var actions = SingleActions.Where(a => (policy.Actions & a) == a && a is not DmAction.ManageAccess).ToList();
        if (actions.Count == 0)
        {
            // Access management within the tenant only: the caller has it, or would not be here.
            return;
        }

        if (policy.Datasources.Count == 0 || policy.Datasources.Any(IsPattern) || policy.Schemas.Any(IsPattern) || policy.ExceptSchemas.Count > 0)
        {
            throw new UnauthorizedAccessException("A tenant's policy names its datasources and schemas, without patterns.");
        }

        var schemas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var datasource in policy.Datasources)
        {
            foreach (var schema in policy.Schemas.Count > 0 ? policy.Schemas.Cast<string?>() : [null])
            {
                var named = schema ?? (_service is DapperMaticService service ? await service.DefaultSchemaOfAsync(datasource).ConfigureAwait(false) : null);
                foreach (var table in policy.Tables)
                {
                    // A table the caller may not read the definition of is refused before it is looked for.
                    if (!_policies.Evaluate(access, new DmResource(datasource, named, table), DmAction.ReadSchema).Allowed)
                    {
                        throw new UnauthorizedAccessException($"This caller may not read {table}, so may not let others use it.");
                    }

                    var found = await _service
                        .GetTableAsync(context, datasource, table, schema, includeColumns: true, includeIndexes: false, includeConstraints: false, cancellationToken)
                        .ConfigureAwait(false);
                    var columns = found.Columns?.Select(c => c.ColumnName).ToList() ?? [];
                    if ((_options.Tenancy.RowsColumns.GetValueOrDefault(table) ?? _options.Tenancy.RowsColumn) is { } marking
                        && !columns.Contains(marking, StringComparer.OrdinalIgnoreCase))
                    {
                        throw new ArgumentException($"{table} has no {marking} column, so a tenant's policy cannot be held to the tenant's rows of it.");
                    }

                    var actual = found.SchemaName is { Length: > 0 } read ? read : named;
                    if (actual is not null)
                    {
                        schemas.Add(actual);
                    }

                    foreach (var action in actions)
                    {
                        var rules = _policies.Evaluate(access, new DmResource(datasource, actual, table, columns), action);
                        if (!rules.Allowed)
                        {
                            throw new UnauthorizedAccessException($"This caller may not {action} {table}, so may not let others.");
                        }

                        // A value the policy writes is written by its callers: in a column the administrator may write.
                        if (rules.Columns is { } writable && action is DmAction.Create or DmAction.Update && policy.Presets.Keys.FirstOrDefault(c => !writable.Contains(c)) is { } preset)
                        {
                            throw new UnauthorizedAccessException($"This caller may not write the column {preset} of {table}, so the policy may not set it.");
                        }

                        if (rules.Columns is { } allowed && (policy.Columns.Count == 0 || policy.Columns.Any(c => !allowed.Contains(c))))
                        {
                            throw new UnauthorizedAccessException(
                                $"This caller may use only some columns of {table} ({string.Join(", ", allowed.Order(StringComparer.OrdinalIgnoreCase))}): the policy names those it gives, among them."
                            );
                        }
                    }
                }
            }
        }

        // Held to the schemas the tables were found in, so that a table of the same name elsewhere is not covered.
        if (policy.Schemas.Count == 0 && schemas.Count > 0)
        {
            policy.Schemas.AddRange(schemas);
        }
    }

    // Whose policies a call is about. A tenant's administrator (whose grants are all within their own tenant) manages
    // their tenant's and no other; another administrator manages the application's, and any tenant's they manage.
    private (DmAccessRules Managing, string? OwnTenant) Scope(DapperMaticOperation context, string? tenant)
    {
        var managing = Managing(context);
        if (managing.Allowed)
        {
            return (managing, null);
        }

        var tenantRules = DmPolicies.Evaluate(_policies.Policies.Where(OwnTenant), Access(context), new DmResource(DmResource.Any, DmResource.Any, null), DmAction.ManageAccess);
        if (!tenantRules.Allowed)
        {
            return (managing, null);
        }

        var own = Access(context).Tenant?.Id.ToString()
            ?? throw new UnauthorizedAccessException("A tenant's administrator manages its policies from a request in the tenant; this one has none.");
        return tenant is null || string.Equals(tenant, own, StringComparison.Ordinal)
            ? (tenantRules, own)
            : throw new UnauthorizedAccessException($"The tenant '{tenant}' is not this caller's.");
    }

    // The policies an administrator sees: of code and configuration, and kept (a tenant's not split by table).
    private IEnumerable<DmPolicy> Kept() => _policies.Configured.Concat(_stored.Policies);

    private DmTenant? TenantOf(string id)
    {
        var settings = _stored.Tenants.GetValueOrDefault(id) ?? _options.Tenancy.Tenants.GetValueOrDefault(id);
        return new DmTenant(id) { DatasourceId = settings?.Datasource, SchemaName = settings?.Schema };
    }

    private static bool IsPattern(string name) => name.Contains('*', StringComparison.Ordinal) || name.Contains('?', StringComparison.Ordinal);

    private static bool Within(DmAccessRules managing, DmPolicy policy) => DmPolicies.IsWithin(managing, policy.Datasources, policy.Schemas);

    private static bool Within(DmAccessRules managing, DapperMaticTenantSettings tenant) =>
        DmPolicies.IsWithin(
            managing,
            tenant.Datasource is { Length: > 0 } datasource ? [datasource] : [],
            tenant.Schema is { Length: > 0 } schema ? [schema] : []
        );

    private static DmAccessContext Access(DapperMaticOperation context) =>
        (context.Access ?? new DmAccessContext(context.User)).ForDatasource(context.DatasourceId);

    // The caller's grants of access management, for every datasource and schema; with permissions of the application's
    // own and no policy about access management, the application's permissions have decided for everything.
    private DmAccessRules Managing(DapperMaticOperation context)
    {
        // Grants within a tenant are not grants of the application's access: a tenant's administrator manages only
        // their tenant's policies (Scope). They still make access management a thing policies are about.
        var everywhere = new DmResource(DmResource.Any, DmResource.Any, null);
        var all = _policies.Evaluate(Access(context), everywhere, DmAction.ManageAccess);
        if (!all.Targeted && _permissions is not DefaultDapperMaticPermissions)
        {
            return DmPolicies.Limit(DmAccessRules.Unrestricted, Access(context), everywhere, DmAction.ManageAccess);
        }

        return DmPolicies.Evaluate(_policies.Policies.Where(p => !OwnTenant(p)), Access(context), everywhere, DmAction.ManageAccess);
    }

    // A change is checked against what is kept now, not as last read: another instance may have changed it since.
    private Task AuthorizeChangeAsync(DapperMaticOperation context, CancellationToken cancellationToken, [CallerMemberName] string method = "")
    {
        _stored.Invalidate();
        return AuthorizeAsync(context, cancellationToken, method);
    }

    private async Task AuthorizeAsync(DapperMaticOperation context, CancellationToken cancellationToken, [CallerMemberName] string method = "")
    {
        ArgumentNullException.ThrowIfNull(context);
        await _stored.RefreshAsync(cancellationToken).ConfigureAwait(false);
        if (!await _permissions.IsAuthorizedAsync(context.WithProperty("CurrentMethod", method)).ConfigureAwait(false))
        {
            throw new UnauthorizedAccessException($"Access denied for operation '{context.Operation}'.");
        }
    }

    private async Task ChangedAsync(DapperMaticOperation context, string message, CancellationToken cancellationToken)
    {
        _stored.Invalidate();
        await _stored.RefreshAsync(cancellationToken).ConfigureAwait(false);
        await _auditLogger.LogOperationAsync(context.ToAuditEvent(true, message, _options.AuditClaims)).ConfigureAwait(false);
    }
}
