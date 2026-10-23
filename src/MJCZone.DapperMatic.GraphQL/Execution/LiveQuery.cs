// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.Json;
using MJCZone.DapperMatic.AspNetCore.Changes;

namespace MJCZone.DapperMatic.GraphQL.Execution;

/// <summary>
/// A live query's stream: its results first, then again each time a table it reads changes (changes close together run
/// it once), sent only when they differ. A run that fails ends the stream with its error. It is told of changes from
/// when it is subscribed to until then: a stream the server never subscribes to (the client completed it while it first
/// ran) holds nothing open.
/// </summary>
internal sealed class LiveQuery : IObservable<object?>
{
    private readonly Func<DapperMaticRealtimeHub.Subscription> _subscribe;
    private readonly List<Dictionary<string, object?>> _first;
    private readonly TimeSpan _debounce;
    private readonly Func<CancellationToken, Task<List<Dictionary<string, object?>>>> _run;
    private readonly Func<List<Dictionary<string, object?>>, object?> _event;
    private readonly Func<Exception, Exception> _error;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveQuery"/> class.
    /// </summary>
    /// <param name="subscribe">Opens the subscription to changes to the tables the query reads.</param>
    /// <param name="first">The query's first results.</param>
    /// <param name="debounce">How long to wait for more changes before running the query again.</param>
    /// <param name="run">Runs the query again, as the caller.</param>
    /// <param name="event">Makes an event of results.</param>
    /// <param name="error">Makes the error a failed run ends the stream with.</param>
    public LiveQuery(
        Func<DapperMaticRealtimeHub.Subscription> subscribe,
        List<Dictionary<string, object?>> first,
        TimeSpan debounce,
        Func<CancellationToken, Task<List<Dictionary<string, object?>>>> run,
        Func<List<Dictionary<string, object?>>, object?> @event,
        Func<Exception, Exception> error
    )
    {
        _subscribe = subscribe;
        _first = first;
        _debounce = debounce;
        _run = run;
        _event = @event;
        _error = error;
    }

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<object?> observer)
    {
        var stop = new CancellationTokenSource();
        var changes = _subscribe();
        _ = RunAsync(observer, changes, stop.Token);
        return new Stop(stop, changes);
    }

    private static string Text(List<Dictionary<string, object?>> rows) => JsonSerializer.Serialize(rows);

    private async Task RunAsync(IObserver<object?> observer, DapperMaticRealtimeHub.Subscription changes, CancellationToken cancellationToken)
    {
        var last = Text(_first);
        observer.OnNext(_event(_first));
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!await changes.WaitAsync(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                await Task.Delay(_debounce, cancellationToken).ConfigureAwait(false);
                changes.Drain();
                var rows = await _run(cancellationToken).ConfigureAwait(false);
                var text = Text(rows);
                if (!string.Equals(text, last, StringComparison.Ordinal))
                {
                    last = text;
                    observer.OnNext(_event(rows));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The client stopped the subscription.
        }
        catch (Exception ex)
        {
            observer.OnError(_error(ex));
        }
    }

    private sealed class Stop(CancellationTokenSource stop, IDisposable changes) : IDisposable
    {
        public void Dispose()
        {
            stop.Cancel();
            stop.Dispose();
            changes.Dispose();
        }
    }
}
