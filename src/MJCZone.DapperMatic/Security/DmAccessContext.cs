// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Security.Claims;

namespace MJCZone.DapperMatic.Security;

/// <summary>
/// Computes a value of an access context when it is first read, such as the ids of the tenants below the request's.
/// </summary>
/// <param name="context">The context the value is read for.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>The value: a scalar, or a list for <c>in</c>.</returns>
public delegate ValueTask<object?> DmValueProvider(DmAccessContext context, CancellationToken cancellationToken);

/// <summary>
/// Who a request is, and for which tenant: what policies decide on and what their filters, checks and presets read as
/// <c>@user.id</c>, <c>@claims.role</c>, <c>@tenant.id</c>, <c>@tenant.below</c> or <c>@values.region</c>.
/// </summary>
public sealed class DmAccessContext
{
    /// <summary>The role every caller who is not signed in has.</summary>
    public const string AnonymousRole = "anonymous";

    /// <summary>The role every signed-in caller has.</summary>
    public const string AuthenticatedRole = "authenticated";

    private readonly ConcurrentDictionary<string, Task<object?>> _computed = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DmAccessContext> _datasources = new(StringComparer.OrdinalIgnoreCase);
    private readonly DmAccessContext? _caller;

    /// <summary>
    /// Initializes a new instance of the <see cref="DmAccessContext"/> class.
    /// </summary>
    /// <param name="user">The caller, or null for an anonymous one.</param>
    /// <param name="services">The services values may use, or null.</param>
    public DmAccessContext(ClaimsPrincipal? user = null, IServiceProvider? services = null)
    {
        User = user ?? new ClaimsPrincipal(new ClaimsIdentity());
        Services = services;
    }

    // The caller's context for one datasource: the caller's own values, computed apart.
    private DmAccessContext(DmAccessContext caller, string datasourceId)
    {
        _caller = caller;
        User = caller.User;
        Services = caller.Services;
        Tenant = caller.Tenant;
        Values = caller.Values;
        Limits = caller.Limits;
        DatasourceId = datasourceId;
    }

    /// <summary>Gets the caller.</summary>
    public ClaimsPrincipal User { get; }

    /// <summary>Gets the services values may use, or null.</summary>
    public IServiceProvider? Services { get; }

    /// <summary>Gets a value indicating whether the caller is signed in.</summary>
    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;

    /// <summary>Gets the tenant the caller acts for, or null for none.</summary>
    public DmTenant? Tenant { get; init; }

    /// <summary>
    /// Gets the most the caller may do, whatever their policies allow, or null for no limits: an API key's. These are
    /// policies of no roles; what the caller may do is what both their policies and these allow (see
    /// <see cref="DmPolicies.Limit"/>).
    /// </summary>
    public IReadOnlyList<DmPolicy>? Limits { get; init; }

    /// <summary>
    /// Gets the datasource the context's values are read for (see <see cref="ForDatasource"/>), or null for none.
    /// </summary>
    public string? DatasourceId { get; }

    /// <summary>
    /// Gets named values for policies, read as <c>@values.name</c>. A value may be a <see cref="DmValueProvider"/>,
    /// computed once when first read.
    /// </summary>
    public Dictionary<string, object?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the context for reading values in a datasource: the same caller, tenant and values, computed once for that
    /// datasource. A value read with SQL reads it from the datasource it is read for, unless it names one of its own.
    /// </summary>
    /// <param name="datasourceId">The datasource, or null for none.</param>
    /// <returns>The context.</returns>
    public DmAccessContext ForDatasource(string? datasourceId)
    {
        var caller = _caller ?? this;
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            return caller;
        }

        return string.Equals(datasourceId, DatasourceId, StringComparison.OrdinalIgnoreCase)
            ? this
            : caller._datasources.GetOrAdd(datasourceId, id => new DmAccessContext(caller, id));
    }

    /// <summary>
    /// Tells whether the caller has a role: <c>anonymous</c> and <c>authenticated</c> as their names say, any other
    /// as the caller's role claims say.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns>True if the caller has it.</returns>
    public bool IsInRole(string role) =>
        role.Equals(AnonymousRole, StringComparison.OrdinalIgnoreCase) ? !IsAuthenticated
        : role.Equals(AuthenticatedRole, StringComparison.OrdinalIgnoreCase) ? IsAuthenticated
        : User.IsInRole(role);

    /// <summary>
    /// Reads a value by its path:
    /// <c>tenant.id</c>, <c>tenant.datasource</c>, <c>tenant.schema</c>, <c>tenant.name</c> (a value of the tenant's);
    /// <c>user.id</c> (the name identifier or <c>sub</c> claim), <c>user.name</c>;
    /// <c>claims.type</c> (the claim's value, or a list when the caller has several);
    /// <c>values.name</c>.
    /// </summary>
    /// <param name="path">The path, with or without a leading <c>@</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The value, or null when there is none.</returns>
    /// <exception cref="ArgumentException">The path does not start with tenant, user, claims or values.</exception>
    public async ValueTask<object?> GetValueAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var trimmed = path.TrimStart('@');
        var dot = trimmed.IndexOf('.', StringComparison.Ordinal);
        var root = (dot < 0 ? trimmed : trimmed[..dot]).ToLowerInvariant();
        var name = dot < 0 ? string.Empty : trimmed[(dot + 1)..];

        switch (root)
        {
            case "tenant":
                if (Tenant is not { } tenant)
                {
                    return null;
                }

                return name.ToLowerInvariant() switch
                {
                    var n when n.Length == 0 || n == "id" => tenant.Id,
                    "datasource" when !tenant.Values.ContainsKey(name) => tenant.DatasourceId,
                    "schema" when !tenant.Values.ContainsKey(name) => tenant.SchemaName,
                    _ => tenant.Values.TryGetValue(name, out var value)
                        ? await ComputeAsync("tenant." + name, value, cancellationToken).ConfigureAwait(false)
                        : null,
                };

            case "user":
                return name.ToLowerInvariant() switch
                {
                    var n when n.Length == 0 || n == "id" => (User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub"))?.Value,
                    "name" => User.Identity?.Name,
                    _ => throw new ArgumentException($"@user has id and name, not {name}.", nameof(path)),
                };

            case "claims":
                var claims = User.FindAll(name).Select(c => c.Value).ToList();
                return claims.Count switch
                {
                    0 => null,
                    1 => claims[0],
                    _ => claims,
                };

            case "values":
                return Values.TryGetValue(name, out var named)
                    ? await ComputeAsync("values." + name, named, cancellationToken).ConfigureAwait(false)
                    : null;

            default:
                throw new ArgumentException(
                    $"'{path}' is not a value: values start with @tenant, @user, @claims or @values.",
                    nameof(path)
                );
        }
    }

    // A provider's value, computed once for the context; any other value as it is.
    private async ValueTask<object?> ComputeAsync(string key, object? value, CancellationToken cancellationToken) =>
        value is DmValueProvider provider
            ? await _computed.GetOrAdd(key, _ => provider(this, cancellationToken).AsTask()).ConfigureAwait(false)
            : value;
}
