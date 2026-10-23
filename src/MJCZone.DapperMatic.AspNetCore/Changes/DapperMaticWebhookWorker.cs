// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.AspNetCore.Security;

namespace MJCZone.DapperMatic.AspNetCore.Changes;

/// <summary>
/// Sends webhooks their messages: for each datasource and webhook, the changes noted since the last one it took, in
/// order, one message each; a failed one is tried again after a growing wait, and dropped after
/// <see cref="DapperMaticWebhookOptions.MaxAttempts"/>. Servers sharing a datasource take turns, by a lease.
/// </summary>
public sealed class DapperMaticWebhookWorker : BackgroundService
{
    /// <summary>The HTTP client webhooks given in code or configuration are sent with.</summary>
    public const string ClientName = "DapperMatic.Webhooks";

    /// <summary>The HTTP client webhooks kept through the API are sent with: it refuses internal addresses.</summary>
    public const string GuardedClientName = "DapperMatic.Webhooks.Guarded";

    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan PruneEvery = TimeSpan.FromMinutes(10);

    private readonly DapperMaticChangeLog _log;
    private readonly IDapperMaticDatasourceRepository _datasources;
    private readonly IDbConnectionFactory _connections;
    private readonly IHttpClientFactory _http;
    private readonly DapperMaticStoredAccess? _stored;
    private readonly DapperMaticWebhookOptions _options;
    private readonly ILogger _logger;
    private readonly string _owner = Guid.NewGuid().ToString("N");
    private readonly ConcurrentDictionary<string, DateTime> _pruned = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticWebhookWorker"/> class.
    /// </summary>
    /// <param name="log">Where the changes are noted.</param>
    /// <param name="datasources">The datasources.</param>
    /// <param name="connections">Makes connections to them.</param>
    /// <param name="http">Makes the HTTP clients messages are sent with.</param>
    /// <param name="options">The options.</param>
    /// <param name="stored">The webhooks kept through the API, refreshed before each round; or null.</param>
    /// <param name="logger">The logger.</param>
    public DapperMaticWebhookWorker(
        DapperMaticChangeLog log,
        IDapperMaticDatasourceRepository datasources,
        IDbConnectionFactory connections,
        IHttpClientFactory http,
        IOptions<DapperMaticOptions> options,
        DapperMaticStoredAccess? stored = null,
        ILogger<DapperMaticWebhookWorker>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        _log = log;
        _datasources = datasources;
        _connections = connections;
        _http = http;
        _stored = stored;
        _options = options.Value.WebhookDelivery;
        _logger = (ILogger?)logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Sends every webhook what is due to it, once: what the worker does every <see cref="DapperMaticWebhookOptions.PollInterval"/>.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        if (_stored is not null)
        {
            await _stored.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }

        var webhooks = _log.Webhooks.Where(w => w.Enabled && !string.IsNullOrWhiteSpace(w.Url)).ToList();
        if (webhooks.Count == 0)
        {
            return;
        }

        foreach (var datasource in await _datasources.GetDatasourcesAsync().ConfigureAwait(false))
        {
            var id = datasource.Id!;
            var hooks = webhooks.Where(w => w.DatasourceId is null || string.Equals(w.DatasourceId, id, StringComparison.OrdinalIgnoreCase)).ToList();
            if (hooks.Count == 0 || datasource.Provider is null || await _datasources.GetConnectionStringAsync(id).ConfigureAwait(false) is not { } connectionString)
            {
                continue;
            }

            try
            {
                using var connection = _connections.CreateConnection(datasource.Provider, connectionString);
                connection.Open();
                await _log.EnsureAsync(id, connection, cancellationToken).ConfigureAwait(false);
                await PruneAsync(id, connection, cancellationToken).ConfigureAwait(false);
                foreach (var hook in hooks)
                {
                    await DeliverAsync(id, connection, hook, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "DapperMatic could not send webhooks of the datasource '{Datasource}'.", id);
            }
        }
    }

    /// <summary>
    /// Makes the handler of the guarded client: it connects only to public addresses, and follows no redirects.
    /// </summary>
    /// <returns>The handler.</returns>
    internal static SocketsHttpHandler GuardedHandler() =>
        new()
        {
            AllowAutoRedirect = false,
            ConnectCallback = async (context, cancellationToken) =>
            {
                var addresses = IPAddress.TryParse(context.DnsEndPoint.Host, out var literal)
                    ? [literal]
                    : await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken).ConfigureAwait(false);
                var allowed = addresses.Where(a => !IsInternal(a)).ToArray();
                if (allowed.Length == 0)
                {
                    throw new HttpRequestException($"The address of {context.DnsEndPoint.Host} is internal, which webhooks kept through the API may not post to.");
                }

                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };

    /// <summary>
    /// Tells whether an address is one of the servers' own network: loopback, private, link-local (cloud metadata
    /// among them), shared, unspecified or multicast.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>True if it is.</returns>
    internal static bool IsInternal(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.None))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || (bytes[0] & 0xFE) == 0xFC;
        }

        var b = address.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254)
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
            || b[0] == 0
            || b[0] >= 224;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (_options.Enabled && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "DapperMatic's webhook worker failed a round.");
            }

            try
            {
                await Task.Delay(_options.PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private static DateTime Now() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    private async Task PruneAsync(string datasourceId, IDbConnection connection, CancellationToken cancellationToken)
    {
        var now = Now();
        if (_pruned.TryGetValue(datasourceId, out var last) && now - last < PruneEvery)
        {
            return;
        }

        _pruned[datasourceId] = now;
        await _log.PruneAsync(connection, now - _options.Retention, cancellationToken).ConfigureAwait(false);
    }

    // Sends one webhook the changes due to it, in order, while its lease is held.
    private async Task DeliverAsync(string datasourceId, IDbConnection connection, DapperMaticWebhook hook, CancellationToken cancellationToken)
    {
        var scope = _log.Scope(connection);
        var key = $"{hook.Tenant}\u001f{hook.Name}";
        var now = Now();
        if (await scope.SingleByIdAsync<DapperMaticWebhookState>(key, cancellationToken: cancellationToken).ConfigureAwait(false) is null)
        {
            // A webhook hears the changes made after it was first seen.
            try
            {
                await scope
                    .InsertAsync(new DapperMaticWebhookState { Webhook = key, ChangeCursor = await _log.LastAsync(connection, cancellationToken).ConfigureAwait(false), UpdatedAt = now }, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (System.Data.Common.DbException)
            {
                // Another server saw it first.
            }
        }

        var until = now + Lease;
        var owner = _owner;
        if (
            await scope
                .PatchAsync<DapperMaticWebhookState>(
                    new { LeaseUntil = until, LeaseOwner = owner },
                    s => s.Webhook == key && (s.LeaseUntil == null || s.LeaseUntil < now || s.LeaseOwner == owner),
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false) == 0
        )
        {
            return;
        }

        var state = (await scope.SingleByIdAsync<DapperMaticWebhookState>(key, cancellationToken: cancellationToken).ConfigureAwait(false))!;
        try
        {
            if (state.NextAttemptAt is { } next && next > now)
            {
                return;
            }

            foreach (var change in await _log.ReadAsync(connection, state.ChangeCursor, 100, cancellationToken).ConfigureAwait(false))
            {
                if (!Hears(hook, datasourceId, change))
                {
                    state.ChangeCursor = change.Id;
                    continue;
                }

                var error = await SendAsync(hook, datasourceId, change, cancellationToken).ConfigureAwait(false);
                if (error is null)
                {
                    (state.ChangeCursor, state.Delivered, state.Attempts, state.NextAttemptAt, state.LastError) = (change.Id, state.Delivered + 1, 0, null, null);
                    continue;
                }

                state.Attempts++;
                state.LastError = error.Length > 2000 ? error[..2000] : error;
                if (state.Attempts >= _options.MaxAttempts)
                {
                    _logger.LogWarning("DapperMatic dropped change {Change} for the webhook '{Webhook}' after {Attempts} attempts: {Error}", change.Id, hook.Name, state.Attempts, error);
                    (state.ChangeCursor, state.Dropped, state.Attempts, state.NextAttemptAt) = (change.Id, state.Dropped + 1, 0, null);
                    continue;
                }

                state.NextAttemptAt = Now() + TimeSpan.FromSeconds(Math.Min(3600, Math.Pow(2, state.Attempts)));
                break;
            }
        }
        finally
        {
            (state.LeaseUntil, state.LeaseOwner, state.UpdatedAt) = (null, null, Now());
            await scope.UpdateAsync(state, cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static bool Hears(DapperMaticWebhook hook, string datasourceId, DapperMaticChange change) =>
        DapperMaticChangeLog.Covers(hook, datasourceId, change.SchemaName, change.TableName)
        && (hook.Tenant is null || string.Equals(hook.Tenant, change.Tenant, StringComparison.Ordinal))
        && change.Action switch
        {
            "insert" => hook.Actions.HasFlag(DapperMaticWebhookActions.Insert),
            "update" => hook.Actions.HasFlag(DapperMaticWebhookActions.Update),
            "delete" => hook.Actions.HasFlag(DapperMaticWebhookActions.Delete),
            _ => hook.Actions.HasFlag(DapperMaticWebhookActions.Write),
        };

    // Posts one change; null if the address took it, else why not.
    private async Task<string?> SendAsync(DapperMaticWebhook hook, string datasourceId, DapperMaticChange change, CancellationToken cancellationToken)
    {
        var message = new JsonObject
        {
            ["id"] = change.Id,
            ["webhook"] = hook.Name,
            ["datasource"] = datasourceId,
            ["schema"] = change.SchemaName,
            ["table"] = change.TableName,
            ["action"] = change.Action,
            ["tenant"] = change.Tenant,
            ["keys"] = change.RowKeys is null ? null : JsonNode.Parse(change.RowKeys),
            ["count"] = change.RowCount,
            ["at"] = DateTime.SpecifyKind(change.CreatedAt, DateTimeKind.Utc),
        };
        var body = Encoding.UTF8.GetBytes(message.ToJsonString());
        using var request = new HttpRequestMessage(HttpMethod.Post, hook.Url) { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        request.Headers.Add("X-DapperMatic-Delivery", change.Id.ToString(CultureInfo.InvariantCulture));
        request.Headers.Add("X-DapperMatic-Event", $"{change.TableName}.{change.Action}");
        request.Headers.Add("X-DapperMatic-Timestamp", timestamp);
        if (!string.IsNullOrEmpty(hook.Secret))
        {
            byte[] signedText = [.. Encoding.UTF8.GetBytes(timestamp + "."), .. body];
            var signed = HMACSHA256.HashData(Encoding.UTF8.GetBytes(hook.Secret), signedText);
            request.Headers.Add("X-DapperMatic-Signature", "sha256=" + Convert.ToHexStringLower(signed));
        }

        foreach (var (name, value) in hook.Headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);
        try
        {
            var client = _http.CreateClient(hook.Source == "stored" && !_options.AllowPrivateAddresses ? GuardedClientName : ClientName);
            using var response = await client.SendAsync(request, timeout.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or SocketException && !cancellationToken.IsCancellationRequested)
        {
            return ex is OperationCanceledException ? $"No answer within {_options.Timeout}." : ex.Message;
        }
    }
}
