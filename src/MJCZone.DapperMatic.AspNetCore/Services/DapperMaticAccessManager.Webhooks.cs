// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Changes;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.AspNetCore.Validation;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Webhooks kept through the API: the application's, by administrators of access; a tenant's, by its administrators,
/// hearing only the tenant's changes.
/// </summary>
public sealed partial class DapperMaticAccessManager
{
    // What a secret reads as when it is shown: it is never given back.
    private const string Masked = "********";

    /// <summary>
    /// Lists the webhooks the caller may manage, their secrets masked: of code and configuration, and kept. A tenant's
    /// administrator sees their tenant's.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="tenant">A tenant whose webhooks to list, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The webhooks.</returns>
    public async Task<IReadOnlyList<DapperMaticWebhook>> GetWebhooksAsync(DapperMaticOperation context, string? tenant = null, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(context, cancellationToken).ConfigureAwait(false);
        var (managing, own) = Scope(context, tenant);
        return own is not null
            ? [.. _stored.Webhooks.Where(w => w.Tenant == own).Select(Shown)]
            : [.. AllWebhooks().Where(w => (tenant is null || w.Tenant == tenant) && Within(managing, w)).Select(Shown)];
    }

    /// <summary>
    /// Gets a webhook by its name, its secret masked.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="name">The webhook's name.</param>
    /// <param name="tenant">The tenant it is one of, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The webhook.</returns>
    /// <exception cref="KeyNotFoundException">No webhook the caller may manage has the name.</exception>
    public async Task<DapperMaticWebhook> GetWebhookAsync(DapperMaticOperation context, string name, string? tenant = null, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(context, cancellationToken).ConfigureAwait(false);
        return Shown(Visible(context, name, tenant));
    }

    /// <summary>
    /// Adds a webhook to those kept, or replaces the kept one of the same name (and tenant); a secret left out, or
    /// masked, keeps the one kept. A tenant's administrator keeps their tenant's: it names its datasource and the
    /// tables, each one the administrator may read, and hears only the tenant's changes. Its address may not be an
    /// internal one unless the options allow it.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="name">The webhook's name.</param>
    /// <param name="webhook">The webhook.</param>
    /// <param name="tenant">The tenant it is one of, or null for the application's.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The webhook kept, its secret masked.</returns>
    /// <exception cref="ArgumentException">The webhook cannot work; the message says why.</exception>
    /// <exception cref="DuplicateKeyException">A webhook of code or configuration has the name.</exception>
    /// <exception cref="UnauthorizedAccessException">The webhook reaches beyond what the caller manages or may read.</exception>
    public async Task<DapperMaticWebhook> SaveWebhookAsync(
        DapperMaticOperation context,
        string name,
        DapperMaticWebhook webhook,
        string? tenant = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(webhook);
        await AuthorizeChangeAsync(context, cancellationToken).ConfigureAwait(false);
        var (managing, own) = Scope(context, tenant);
        webhook.Name = name;
        webhook.Tenant = own ?? tenant;
        webhook.Source = null;
        if (!Uri.TryCreate(webhook.Url, UriKind.Absolute, out var url) || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp) || url.UserInfo.Length > 0)
        {
            throw new ArgumentException($"The webhook '{name}' needs an http or https address, without credentials in it.");
        }

        if (webhook.Actions == DapperMaticWebhookActions.None)
        {
            throw new ArgumentException($"The webhook '{name}' hears nothing: give its actions.");
        }

        var kept = _stored.Webhooks.FirstOrDefault(w => DapperMaticAccessEdits.Same(w, name, webhook.Tenant));
        if (webhook.Tenant is null)
        {
            if (_options.Webhooks.Exists(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase) && w.Tenant is null))
            {
                throw new DuplicateKeyException($"The webhook '{name}' is given in code or configuration, and is changed there.");
            }

            if (!Within(managing, webhook) || (kept is not null && !Within(managing, kept)))
            {
                throw new UnauthorizedAccessException($"The webhook '{name}' reaches datasources or schemas this caller does not manage access to: name them, without patterns.");
            }
        }
        else
        {
            // A tenant's webhook is held to the tenant's datasource and schema, as its policies are.
            var settings = own is not null ? Access(context).Tenant : TenantOf(webhook.Tenant);
            webhook.DatasourceId = settings?.DatasourceId ?? webhook.DatasourceId;
            if (settings?.SchemaName is { Length: > 0 } schema)
            {
                webhook.Schemas.Clear();
                webhook.Schemas.Add(schema);
            }

            if (own is not null)
            {
                AssertMayRead(context, webhook);
            }
            else if (!Within(managing, webhook) || (kept is not null && !Within(managing, kept)))
            {
                throw new UnauthorizedAccessException($"The webhook '{name}' reaches datasources or schemas this caller does not manage access to.");
            }
        }

        // A secret not given again is kept; what is kept is encrypted when there is a key.
        if (webhook.Secret is null or Masked)
        {
            webhook.Secret = kept?.Secret;
        }

        var keeping = Copy(webhook);
        keeping.Secret = _stored.Hide(webhook.Secret);
        await _stored.Repository.SaveWebhookAsync(keeping, cancellationToken).ConfigureAwait(false);
        await ChangedAsync(context, $"Saved the webhook '{name}'" + (webhook.Tenant is null ? string.Empty : $" of the tenant '{webhook.Tenant}'"), cancellationToken)
            .ConfigureAwait(false);
        return Shown(webhook);
    }

    /// <summary>
    /// Removes a webhook kept by DapperMatic.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="name">The webhook's name.</param>
    /// <param name="tenant">The tenant it is one of, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    /// <exception cref="KeyNotFoundException">No kept webhook the caller may manage has the name.</exception>
    /// <exception cref="DuplicateKeyException">The webhook is given in code or configuration.</exception>
    public async Task DeleteWebhookAsync(DapperMaticOperation context, string name, string? tenant = null, CancellationToken cancellationToken = default)
    {
        await AuthorizeChangeAsync(context, cancellationToken).ConfigureAwait(false);
        var (managing, own) = Scope(context, tenant);
        var key = own ?? tenant;
        if (key is null && _options.Webhooks.Exists(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase) && w.Tenant is null))
        {
            throw new DuplicateKeyException($"The webhook '{name}' is given in code or configuration, and is removed there.");
        }

        if (_stored.Webhooks.FirstOrDefault(w => DapperMaticAccessEdits.Same(w, name, key)) is not { } kept || (own is null && !Within(managing, kept)))
        {
            throw new KeyNotFoundException($"No webhook is named '{name}'.");
        }

        await _stored.Repository.DeleteWebhookAsync(name, key, cancellationToken).ConfigureAwait(false);
        await ChangedAsync(context, $"Removed the webhook '{name}'" + (key is null ? string.Empty : $" of the tenant '{key}'"), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads where a webhook's messages have got to, in each datasource it hears: the messages taken and dropped, the
    /// attempts at the one waiting, and its last error.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="name">The webhook's name.</param>
    /// <param name="tenant">The tenant it is one of, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Its state in each datasource, by datasource.</returns>
    /// <exception cref="KeyNotFoundException">No webhook the caller may manage has the name.</exception>
    public async Task<IReadOnlyDictionary<string, DapperMaticWebhookStatus>> GetWebhookStatusAsync(
        DapperMaticOperation context,
        string name,
        string? tenant = null,
        CancellationToken cancellationToken = default
    )
    {
        await AuthorizeAsync(context, cancellationToken).ConfigureAwait(false);
        var webhook = Visible(context, name, tenant);
        var status = new Dictionary<string, DapperMaticWebhookStatus>(StringComparer.OrdinalIgnoreCase);
        if (_changes is null || _datasources is null || _connections is null)
        {
            return status;
        }

        foreach (var datasource in await _datasources.GetDatasourcesAsync().ConfigureAwait(false))
        {
            if ((webhook.DatasourceId is not null && !string.Equals(webhook.DatasourceId, datasource.Id, StringComparison.OrdinalIgnoreCase))
                || datasource.Provider is null
                || await _datasources.GetConnectionStringAsync(datasource.Id!).ConfigureAwait(false) is not { } connectionString)
            {
                continue;
            }

            using var connection = _connections.CreateConnection(datasource.Provider, connectionString);
            connection.Open();
            if (!await connection.DoesTableExistAsync(_changes.Schema, _changes.StateTable, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            if (await _changes.Scope(connection).SingleByIdAsync<DapperMaticWebhookState>($"{webhook.Tenant}\u001f{webhook.Name}", cancellationToken: cancellationToken).ConfigureAwait(false) is { } state)
            {
                status[datasource.Id!] = new DapperMaticWebhookStatus
                {
                    LastChange = state.ChangeCursor,
                    Delivered = state.Delivered,
                    Dropped = state.Dropped,
                    Attempts = state.Attempts,
                    NextAttemptAt = state.NextAttemptAt is { } next ? DateTime.SpecifyKind(next, DateTimeKind.Utc) : null,
                    LastError = state.LastError,
                };
            }
        }

        return status;
    }

    // A webhook as shown: its secret masked.
    private static DapperMaticWebhook Shown(DapperMaticWebhook webhook)
    {
        var shown = Copy(webhook);
        shown.Secret = webhook.Secret is null ? null : Masked;
        return shown;
    }

    private static DapperMaticWebhook Copy(DapperMaticWebhook webhook)
    {
        var copy = new DapperMaticWebhook
        {
            Name = webhook.Name,
            Url = webhook.Url,
            Secret = webhook.Secret,
            DatasourceId = webhook.DatasourceId,
            Actions = webhook.Actions,
            Tenant = webhook.Tenant,
            Enabled = webhook.Enabled,
            Source = webhook.Source,
        };
        copy.Schemas.AddRange(webhook.Schemas);
        copy.Tables.AddRange(webhook.Tables);
        foreach (var (header, value) in webhook.Headers)
        {
            copy.Headers[header] = value;
        }

        return copy;
    }

    private static bool Within(DmAccessRules managing, DapperMaticWebhook webhook) =>
        DmPolicies.IsWithin(managing, webhook.DatasourceId is { Length: > 0 } datasource ? [datasource] : [], webhook.Schemas);

    // The webhooks an administrator sees: of code and configuration, and kept.
    private IEnumerable<DapperMaticWebhook> AllWebhooks() => _options.Webhooks.Select(Configured).Concat(_stored.Webhooks);

    private static DapperMaticWebhook Configured(DapperMaticWebhook webhook)
    {
        var copy = Copy(webhook);
        copy.Source ??= "code";
        return copy;
    }

    private DapperMaticWebhook Visible(DapperMaticOperation context, string name, string? tenant)
    {
        var (managing, own) = Scope(context, tenant);
        var key = own ?? tenant;
        return AllWebhooks().FirstOrDefault(w => DapperMaticAccessEdits.Same(w, name, key)) is { } webhook && (own is not null || Within(managing, webhook))
            ? webhook
            : throw new KeyNotFoundException($"No webhook is named '{name}'.");
    }

    // A tenant's administrator names the datasource and each table, and reads every one whole: a webhook tells of each
    // of the tenant's rows that changes, with its key, so a filter or a column list of theirs would be passed over.
    private void AssertMayRead(DapperMaticOperation context, DapperMaticWebhook webhook)
    {
        if (webhook.DatasourceId is not { Length: > 0 } datasource || webhook.Tables.Count == 0 || webhook.Tables.Exists(IsPattern))
        {
            throw new UnauthorizedAccessException($"A tenant's webhook names its datasource and its tables, without patterns.");
        }

        var schema = webhook.Schemas.Count == 1 ? webhook.Schemas[0] : DmResource.Any;
        foreach (var table in webhook.Tables)
        {
            var rules = _policies.Evaluate(Access(context), new DmResource(datasource, schema, table), DmAction.Read);
            if (!rules.Allowed)
            {
                throw new UnauthorizedAccessException($"The webhook '{webhook.Name}' hears the table {table}, which this caller may not read.");
            }

            if (rules.Columns is not null
                || !rules.Permissive.Any(p => _policies.OnlyFences(table, p.Filter))
                || !rules.Restrictive.All(r => _policies.OnlyFences(table, r.Filter)))
            {
                throw new UnauthorizedAccessException(
                    $"The webhook '{webhook.Name}' hears the table {table}, which this caller reads only in part: a webhook tells of every row of the tenant's that changes."
                );
            }
        }
    }
}
