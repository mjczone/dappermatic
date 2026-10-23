// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Repositories;

namespace MJCZone.DapperMatic.AspNetCore.Changes;

/// <summary>
/// Tells this server's subscriptions of the changes other servers made: the change table of each datasource
/// subscriptions are open on is read every <see cref="Models.DapperMaticRealtimeOptions.PollInterval"/>.
/// </summary>
public sealed class DapperMaticRealtimePoller : BackgroundService
{
    private readonly DapperMaticRealtimeHub _hub;
    private readonly DapperMaticChangeLog _log;
    private readonly IDapperMaticDatasourceRepository _datasources;
    private readonly IDbConnectionFactory _connections;
    private readonly DapperMaticOptions _options;
    private readonly ILogger _logger;
    private readonly Dictionary<string, long> _cursors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticRealtimePoller"/> class.
    /// </summary>
    /// <param name="hub">The subscriptions open on this server.</param>
    /// <param name="log">Where the changes are noted.</param>
    /// <param name="datasources">The datasources.</param>
    /// <param name="connections">Makes connections to them.</param>
    /// <param name="options">The options.</param>
    /// <param name="logger">The logger.</param>
    public DapperMaticRealtimePoller(
        DapperMaticRealtimeHub hub,
        DapperMaticChangeLog log,
        IDapperMaticDatasourceRepository datasources,
        IDbConnectionFactory connections,
        IOptions<DapperMaticOptions> options,
        ILogger<DapperMaticRealtimePoller>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        _hub = hub;
        _log = log;
        _datasources = datasources;
        _connections = connections;
        _options = options.Value;
        _logger = (ILogger?)logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Reads the changes noted since the last round, once, and tells the subscriptions they concern.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var open = _hub.Datasources;
        foreach (var gone in _cursors.Keys.Where(k => !open.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList())
        {
            _cursors.Remove(gone);
        }

        foreach (var datasourceId in open)
        {
            try
            {
                if (await _datasources.GetDatasourceAsync(datasourceId).ConfigureAwait(false) is not { Provider: { } provider }
                    || await _datasources.GetConnectionStringAsync(datasourceId).ConfigureAwait(false) is not { } connectionString)
                {
                    continue;
                }

                using var connection = _connections.CreateConnection(provider, connectionString);
                connection.Open();
                await _log.EnsureAsync(datasourceId, connection, cancellationToken).ConfigureAwait(false);
                if (!_cursors.TryGetValue(datasourceId, out var cursor))
                {
                    _cursors[datasourceId] = await _log.LastAsync(connection, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var changes = await _log.ReadAsync(connection, cursor, 1000, cancellationToken).ConfigureAwait(false);
                if (changes.Count > 0)
                {
                    _hub.Notify(datasourceId, changes.Select(c => (c.SchemaName, c.TableName)));
                    _cursors[datasourceId] = changes[^1].Id;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "DapperMatic could not read the changes of the datasource '{Datasource}' for subscriptions.", datasourceId);
            }
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            try
            {
                await Task.Delay(_options.Realtime.PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
