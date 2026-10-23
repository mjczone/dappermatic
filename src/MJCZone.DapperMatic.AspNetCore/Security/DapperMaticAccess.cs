// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Authentication;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Security;

/// <summary>
/// Builds callers' sessions (<see cref="DapperMaticCallerSession"/>): who is calling, for which tenant, with which
/// values, from a request and the tenancy settings; and holds an operation to the caller's tenant.
/// </summary>
public sealed partial class DapperMaticAccess
{
    private readonly DapperMaticOptions _options;
    private readonly DapperMaticStoredAccess? _stored;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticAccess"/> class.
    /// </summary>
    /// <param name="options">The DapperMatic options, with the tenancy settings.</param>
    /// <param name="stored">The tenants kept by DapperMatic, which win over those of configuration; null for none.</param>
    public DapperMaticAccess(IOptions<DapperMaticOptions> options, DapperMaticStoredAccess? stored = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _stored = stored;
    }

    /// <summary>
    /// Gets a request's session: the one built for it, or one built now and kept for the rest of the request. A session
    /// built before the request's caller was known (before authentication ran) is built again for them.
    /// </summary>
    /// <param name="http">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The session.</returns>
    public async ValueTask<DapperMaticCallerSession> GetSessionAsync(HttpContext http, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(http);
        if (DapperMaticCallerSession.Of(http) is { } session && ReferenceEquals(session.SignedIn, http.User))
        {
            return session;
        }

        session = await BuildAsync(http, cancellationToken).ConfigureAwait(false);
        http.Features.Set(session);
        return session;
    }

    /// <summary>
    /// Builds a session for the caller of a request, <see cref="HttpContext.User"/>: its tenant, found by the tenancy
    /// sources and completed by its settings, values and lookups. A socket's caller signs in after the request that
    /// opened it: the session is built once the request's user is set to them.
    /// </summary>
    /// <param name="http">The request, with its caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The session.</returns>
    public ValueTask<DapperMaticCallerSession> BuildAsync(HttpContext http, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(http);
        return BuildAsync(http, DateTimeOffset.UtcNow, cancellationToken);
    }

    /// <summary>
    /// Checks a session before it is used again: reads what DapperMatic keeps again if it is due, and builds the session
    /// again when its tenant is now kept differently.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The session to use.</returns>
    internal async ValueTask<DapperMaticCallerSession> CheckAsync(DapperMaticCallerSession session, CancellationToken cancellationToken)
    {
        // A key revoked, an account turned off: the session ends.
        await CheckCallerAsync(session.SignedIn, session.Request, again: true, cancellationToken).ConfigureAwait(false);
        if (_stored is null)
        {
            return session;
        }

        await _stored.RefreshAsync(cancellationToken).ConfigureAwait(false);
        var now = session.Tenant is { } tenant ? _stored.Tenants.GetValueOrDefault(tenant.ToString()) : null;
        return SameSettings(now, session.StoredTenant)
            ? session
            : await BuildAsync(session.Request, session.StartedAt, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Holds an operation to its caller's tenant: a datasource or schema it names must be the tenant's, and naming no
    /// schema uses the tenant's.
    /// </summary>
    /// <param name="session">The caller's session.</param>
    /// <param name="operation">The operation, with the datasource and schema it names.</param>
    /// <returns>The operation as the caller's: with their access context, in the schema it acts in.</returns>
    /// <exception cref="UnauthorizedAccessException">The operation needs a tenant and has none, or names another tenant's datasource or schema.</exception>
    internal DapperMaticOperation Hold(DapperMaticCallerSession session, DapperMaticOperation operation)
    {
        var schemaName = operation.SchemaName is { Length: > 0 } s && s != "_" ? s : null;
        if (operation.DatasourceId is not null && _options.Tenancy.RequireTenant && session.Tenant is null)
        {
            throw new UnauthorizedAccessException("This request needs a tenant, and none was found in it.");
        }

        Hold(session.Access, operation.DatasourceId, schemaName);
        return operation with
        {
            User = session.User,
            Access = session.Access.ForDatasource(operation.DatasourceId),
            SchemaName = schemaName ?? session.Tenant?.SchemaName,
        };
    }

    /// <summary>
    /// Holds a request to its tenant: a datasource or schema it names, in its URL, body or query, must be the tenant's.
    /// </summary>
    /// <param name="context">The request's access context, with its tenant.</param>
    /// <param name="datasourceId">The datasource the request names, or null.</param>
    /// <param name="schemaName">The schema the request names, or null.</param>
    /// <exception cref="UnauthorizedAccessException">The request names another tenant's datasource or schema.</exception>
    internal static void Hold(DmAccessContext context, string? datasourceId, string? schemaName)
    {
        if (context.Tenant is not { } tenant)
        {
            return;
        }

        if (datasourceId is { Length: > 0 } && tenant.DatasourceId is { } own && !own.Equals(datasourceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"The datasource {datasourceId} is not this tenant's.");
        }

        if (schemaName is { Length: > 0 } and not "_" && tenant.SchemaName is { } ownSchema && !ownSchema.Equals(schemaName, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"The schema {schemaName} is not this tenant's.");
        }
    }

    /// <summary>
    /// Reads a tenant id from one source of a request.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="http">The request.</param>
    /// <returns>The id, or null if the request does not have one there.</returns>
    internal static string? Read(DapperMaticTenantSource source, HttpContext http)
    {
        if (source.Claim is { Length: > 0 } claim)
        {
            return http.User.FindFirst(claim)?.Value;
        }

        if (source.Header is { Length: > 0 } header)
        {
            return http.Request.Headers[header].FirstOrDefault();
        }

        if (source.Route is { Length: > 0 } route)
        {
            return http.GetRouteValue(route)?.ToString();
        }

        if (source.Query is { Length: > 0 } query)
        {
            return http.Request.Query[query].FirstOrDefault();
        }

        if (source.Subdomain is { Length: > 0 } pattern)
        {
            var expression = "^" + Regex.Escape(pattern).Replace(@"\{tenant}", "(?<tenant>[^.]+)", StringComparison.OrdinalIgnoreCase) + "$";
            var match = Regex.Match(http.Request.Host.Host, expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return match.Success ? match.Groups["tenant"].Value : null;
        }

        return null;
    }

    /// <summary>
    /// Replaces each <c>@tenant.x</c>, <c>@user.x</c>, <c>@claims.x</c> and <c>@values.x</c> of a query by a parameter
    /// holding the context's value.
    /// </summary>
    /// <param name="sql">The query.</param>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The query and its parameters.</returns>
    internal static async Task<(string Sql, DynamicParameters Parameters)> BindAsync(string sql, DmAccessContext context, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ContextPath().Matches(sql))
        {
            if (!names.ContainsKey(match.Value))
            {
                var name = $"dm_v{names.Count}";
                names[match.Value] = name;
                parameters.Add(name, await context.GetValueAsync(match.Value, cancellationToken).ConfigureAwait(false));
            }
        }

        return (ContextPath().Replace(sql, m => "@" + names[m.Value]), parameters);
    }

    // Whether a tenant is kept as it was: the same datasource, schema and values.
    private static bool SameSettings(DapperMaticTenantSettings? now, DapperMaticTenantSettings? then) =>
        ReferenceEquals(now, then)
        || (now is not null
            && then is not null
            && string.Equals(now.Datasource, then.Datasource, StringComparison.OrdinalIgnoreCase)
            && string.Equals(now.Schema, then.Schema, StringComparison.OrdinalIgnoreCase)
            && now.Values.Count == then.Values.Count
            && now.Values.All(v => then.Values.TryGetValue(v.Key, out var value) && string.Equals(v.Value, value, StringComparison.Ordinal)));

    private async ValueTask<DapperMaticCallerSession> BuildAsync(HttpContext http, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        if (_stored is not null)
        {
            await _stored.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }

        var tenancy = _options.Tenancy;

        // A caller DapperMatic signed in (a key, an account, a ticket) names their tenant themselves: the request is not
        // read for another.
        object? id = null;
        if (DapperMaticClaims.TryGetTenant(http.User, out var named))
        {
            id = named;
        }
        else
        {
            foreach (var source in tenancy.CodeSources)
            {
                id = Present(await source(http, cancellationToken).ConfigureAwait(false));
                if (id is not null)
                {
                    break;
                }
            }

            if (id is null)
            {
                id = tenancy.Sources.Select(source => Present(Read(source, http))).FirstOrDefault(found => found is not null);
            }
        }

        DmTenant? found = null;
        DapperMaticTenantSettings? stored = null;
        if (id is not null)
        {
            var tenant = new DmTenant(id);
            stored = _stored?.Tenants.GetValueOrDefault(tenant.ToString());
            if (stored is { } settings || tenancy.Tenants.TryGetValue(tenant.ToString(), out settings))
            {
                tenant.DatasourceId = settings.Datasource;
                tenant.SchemaName = settings.Schema;
                foreach (var (name, value) in settings.Values)
                {
                    tenant.Values[name] = value;
                }
            }

            foreach (var (name, value) in tenancy.Values)
            {
                tenant.Values.TryAdd(name, value.Sql is { Length: > 0 } sql ? SqlValue(sql, value.Datasource) : value.Value);
            }

            foreach (var (name, provider) in tenancy.CodeValues)
            {
                tenant.Values[name] = provider;
            }

            foreach (var lookup in tenancy.Lookups)
            {
                await lookup(tenant, http, cancellationToken).ConfigureAwait(false);
            }

            found = tenant;
        }

        // The roles found besides the role claim, as an identity of their own (signing no one in): the application's
        // user is left as it is.
        var user = http.User;
        var roles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in _options.RoleSources)
        {
            roles.UnionWith(await source.ReadAsync(http, user, cancellationToken).ConfigureAwait(false));
        }

        if (roles.Count > 0)
        {
            user = new ClaimsPrincipal(user.Identities.Append(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), null, ClaimTypes.Name, ClaimTypes.Role)));
        }

        var terms = await CheckCallerAsync(http.User, http, again: false, cancellationToken).ConfigureAwait(false);
        var context = new DmAccessContext(user, http.RequestServices) { Tenant = found, Limits = terms.Limits };
        foreach (var (name, provider) in _options.RequestValues)
        {
            context.Values[name] = provider;
        }

        var signedInUntil = http.Features.Get<DapperMaticSignedIn>()?.Until;
        var endsAt = signedInUntil is { } until && (terms.EndsAt is null || until < terms.EndsAt) ? until : terms.EndsAt;
        return new DapperMaticCallerSession(user, context, http, this, stored, startedAt, _options.MaxSessionAge, http.User, endsAt);
    }

    // The checks of callers DapperMatic signed in (keys, accounts): refused, or held to their limits and end.
    private static async ValueTask<DapperMaticCallerTerms> CheckCallerAsync(ClaimsPrincipal user, HttpContext http, bool again, CancellationToken cancellationToken)
    {
        List<DmPolicy>? limits = null;
        DateTimeOffset? endsAt = null;
        foreach (var check in http.RequestServices?.GetServices<IDapperMaticCallerCheck>() ?? [])
        {
            if (await check.CheckAsync(user, http, again, cancellationToken).ConfigureAwait(false) is not { } terms)
            {
                continue;
            }

            if (terms.Limits is { } some)
            {
                (limits ??= []).AddRange(some);
            }

            if (terms.EndsAt is { } ends && (endsAt is null || ends < endsAt))
            {
                endsAt = ends;
            }
        }

        return new DapperMaticCallerTerms(limits, endsAt);
    }

    // An id that is there: an empty string is none.
    private static object? Present(object? id) => id is null || (id is string s && string.IsNullOrWhiteSpace(s)) ? null : id;

    // A value read with SQL when first used, with the context's values as parameters.
    private static DmValueProvider SqlValue(string sql, string? datasource) =>
        async (context, cancellationToken) =>
        {
            var services = context.Services ?? throw new InvalidOperationException("A value read with SQL needs the request's services.");
            var datasourceId = datasource ?? context.DatasourceId
                ?? throw new InvalidOperationException("A value read with SQL needs a datasource: name one, or read it in a request for one.");
            var repository = services.GetRequiredService<IDapperMaticDatasourceRepository>();
            var found = await repository.GetDatasourceAsync(datasourceId).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"Datasource '{datasourceId}' not found.");
            var connectionString = await repository.GetConnectionStringAsync(datasourceId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Connection string for datasource '{datasourceId}' is not available.");
            using var db = services.GetRequiredService<IDbConnectionFactory>().CreateConnection(found.Provider!, connectionString);

            var (bound, parameters) = await BindAsync(sql, context, cancellationToken).ConfigureAwait(false);
            var rows = await db.QueryAsync(new CommandDefinition(bound, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);
            var values = rows.Select(row => ((IDictionary<string, object?>)row).Values.FirstOrDefault()).ToList();
            return values.Count switch
            {
                0 => null,
                1 => values[0],
                _ => values,
            };
        };

    [GeneratedRegex(@"@(tenant|user|claims|values)(\.[A-Za-z_][A-Za-z0-9_]*)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex ContextPath();
}
