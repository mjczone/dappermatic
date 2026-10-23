// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// What subscriptions to queries hear.
/// </summary>
public partial class DapperMaticService
{
    /// <summary>
    /// Finds the tables whose changes run a subscription's query again: those the query read, a view's as the options
    /// list them. Each must be one callers may subscribe to.
    /// </summary>
    /// <param name="context">The operation context of the query, run once.</param>
    /// <param name="datasourceId">The datasource.</param>
    /// <returns>The tables, by schema (as resolved) and name.</returns>
    /// <exception cref="ArgumentException">The query reads a table or view callers may not subscribe to.</exception>
    internal List<(string? Schema, string Table)> SubscribedTables(DapperMaticOperation context, string datasourceId)
    {
        if (context.State.Reads is not { } reads)
        {
            throw new InvalidOperationException("The query left no record of what it read.");
        }

        var tables = new List<(string? Schema, string Table)>();
        foreach (var (schema, name, view) in reads)
        {
            var entry = _changeLog?.RealtimeTable(datasourceId, schema, name)
                ?? throw new ArgumentException($"{(view ? "The view" : "The table")} {name} is not one callers may subscribe to: list it in the options' RealtimeTables.");
            if (!view)
            {
                tables.Add((schema, name));
                continue;
            }

            if (entry.Reads.Length == 0)
            {
                throw new ArgumentException($"The view {name} names no tables it reads, whose changes would run its subscriptions again: give them in RealtimeTables' Reads.");
            }

            foreach (var read in entry.Reads)
            {
                var dot = read.LastIndexOf('.');
                tables.Add((dot > 0 ? read[..dot] : schema, read[(dot + 1)..]));
            }
        }

        return tables;
    }
}
