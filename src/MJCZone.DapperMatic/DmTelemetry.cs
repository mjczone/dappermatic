// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MJCZone.DapperMatic;

/// <summary>
/// DapperMatic's traces and metrics, through .NET's own <see cref="ActivitySource"/> and <see cref="Meter"/>: an
/// application collects them with the OpenTelemetry SDK it uses, by name.
/// </summary>
/// <example>
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithTracing(t => t.AddSource(DmTelemetry.Name))
///     .WithMetrics(m => m.AddMeter(DmTelemetry.Name));
/// </code>
/// </example>
/// <remarks>
/// A statement's span carries its SQL with parameters by name (<c>@p0</c>), never their values; the operation, the
/// table, and the rows read or written.
/// </remarks>
public static class DmTelemetry
{
    /// <summary>The name of DapperMatic's activity source and meter.</summary>
    public const string Name = "MJCZone.DapperMatic";

    /// <summary>Gets the activity source spans are started from.</summary>
    internal static ActivitySource Source { get; } = new(Name, typeof(DmTelemetry).Assembly.GetName().Version?.ToString());

    /// <summary>Gets the meter instruments are made by.</summary>
    internal static Meter Meter { get; } = new(Name, typeof(DmTelemetry).Assembly.GetName().Version?.ToString());

    /// <summary>Gets the duration of the statements the web API's engine runs, in milliseconds.</summary>
    internal static Histogram<double> StatementDuration { get; } =
        Meter.CreateHistogram<double>("dappermatic.statement.duration", "ms", "How long the statements DapperMatic runs take.");

    /// <summary>Gets the count of statements looked for in the result cache, by whether they were found.</summary>
    internal static Counter<long> CacheRequests { get; } =
        Meter.CreateCounter<long>("dappermatic.cache.requests", "{statement}", "Statements looked for in DapperMatic's result cache, found (hit) or not (miss).");

    /// <summary>
    /// Runs a statement in a span of its own: the SQL with parameters by name, the operation and the table.
    /// </summary>
    /// <typeparam name="T">The type of what the statement returns.</typeparam>
    /// <param name="provider">The database, as OpenTelemetry names it (<c>postgresql</c>, <c>mssql</c>, ...).</param>
    /// <param name="table">The table the statement is about.</param>
    /// <param name="sql">The SQL, with parameters by name.</param>
    /// <param name="run">Runs the statement.</param>
    /// <returns>What the statement returned.</returns>
    internal static async Task<T> TracedAsync<T>(string provider, string table, string sql, Func<Task<T>> run)
    {
        var operation = sql.TrimStart().Split(' ', 2)[0].ToUpperInvariant();
        using var activity = Source.StartActivity($"{operation} {table}", ActivityKind.Client);
        activity?.SetTag("db.system.name", provider);
        activity?.SetTag("db.operation.name", operation);
        activity?.SetTag("db.collection.name", table);
        activity?.SetTag("db.query.text", sql);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await run().ConfigureAwait(false);
            switch (result)
            {
                case int affected:
                    activity?.SetTag("db.response.affected_rows", affected);
                    break;
                case System.Collections.ICollection rows:
                    activity?.SetTag("db.response.returned_rows", rows.Count);
                    break;
            }

            return result;
        }
        catch (Exception e)
        {
            activity?.SetStatus(ActivityStatusCode.Error, e.GetType().Name);
            activity?.SetTag("error.type", e.GetType().FullName);
            throw;
        }
        finally
        {
            StatementDuration.Record(
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                new KeyValuePair<string, object?>("db.system.name", provider),
                new KeyValuePair<string, object?>("db.operation.name", operation)
            );
        }
    }
}
