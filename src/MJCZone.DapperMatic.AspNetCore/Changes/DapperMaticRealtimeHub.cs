// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Threading.Channels;

namespace MJCZone.DapperMatic.AspNetCore.Changes;

/// <summary>
/// The subscriptions open on this server, each told when a table its query reads changes: at once for a change made
/// on this server, and within the poll interval for one another server made.
/// </summary>
public sealed class DapperMaticRealtimeHub
{
    private readonly ConcurrentDictionary<Subscription, byte> _subscriptions = new();
    private long _notified;

    /// <summary>Gets the datasources subscriptions are open on.</summary>
    internal IReadOnlyCollection<string> Datasources =>
        [.. _subscriptions.Keys.Select(s => s.DatasourceId).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Gets how many times subscriptions have been told of changes, on any datasource: taken before a query first runs,
    /// and given to <see cref="Subscribe"/> when it is opened later, so that a change between the two is not missed.
    /// </summary>
    internal long Notified => Interlocked.Read(ref _notified);

    /// <summary>
    /// Opens a subscription to tables' changes.
    /// </summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="tables">The tables, by schema (as resolved) and name.</param>
    /// <param name="since">
    /// The <see cref="Notified"/> taken when the query first ran, or null: if changes were told since, the subscription
    /// starts signalled (a change elsewhere runs the query once more for nothing, never a change missed).
    /// </param>
    /// <returns>The subscription, to read and to close.</returns>
    internal Subscription Subscribe(string datasourceId, IEnumerable<(string? Schema, string Table)> tables, long? since = null)
    {
        var subscription = new Subscription(this, datasourceId, [.. tables.Select(t => Key(t.Schema, t.Table))]);
        _subscriptions[subscription] = 0;
        if (since is { } then && Notified != then)
        {
            subscription.Signal();
        }

        return subscription;
    }

    /// <summary>
    /// Tells the subscriptions that read tables that they changed.
    /// </summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="tables">The tables, by schema (as resolved) and name.</param>
    internal void Notify(string datasourceId, IEnumerable<(string? Schema, string Table)> tables)
    {
        var changed = tables.Select(t => Key(t.Schema, t.Table)).ToHashSet(StringComparer.Ordinal);
        Interlocked.Increment(ref _notified);
        foreach (var subscription in _subscriptions.Keys)
        {
            if (string.Equals(subscription.DatasourceId, datasourceId, StringComparison.OrdinalIgnoreCase) && subscription.Tables.Overlaps(changed))
            {
                subscription.Signal();
            }
        }
    }

    private static string Key(string? schemaName, string tableName) => $"{schemaName?.ToLowerInvariant()}\u001f{tableName.ToLowerInvariant()}";

    /// <summary>
    /// A subscription: signalled, at most once until read, when a table it reads changes.
    /// </summary>
    internal sealed class Subscription : IDisposable
    {
        private readonly DapperMaticRealtimeHub _hub;
        private readonly Channel<bool> _signals = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="hub">The hub it is open in.</param>
        /// <param name="datasourceId">The datasource.</param>
        /// <param name="tables">The tables read, as keys.</param>
        internal Subscription(DapperMaticRealtimeHub hub, string datasourceId, HashSet<string> tables)
        {
            _hub = hub;
            DatasourceId = datasourceId;
            Tables = tables;
        }

        /// <summary>Gets the datasource.</summary>
        public string DatasourceId { get; }

        /// <summary>Gets the tables read, as keys.</summary>
        public HashSet<string> Tables { get; }

        /// <summary>
        /// Waits for a change, or for a time to pass.
        /// </summary>
        /// <param name="timeout">How long to wait.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if a table changed; false if the time passed.</returns>
        public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            using var timer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timer.CancelAfter(timeout);
            try
            {
                await _signals.Reader.ReadAsync(timer.Token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }

        /// <summary>Drains a signal sent while the query ran, so that one change is not run for twice.</summary>
        public void Drain() => _signals.Reader.TryRead(out _);

        /// <inheritdoc />
        public void Dispose() => _hub._subscriptions.TryRemove(this, out _);

        /// <summary>Signals a change, unless one is already waiting to be read.</summary>
        internal void Signal() => _signals.Writer.TryWrite(true);
    }
}
