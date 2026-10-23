// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Dml.Internal;

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// Loading relations onto rows already read.
/// </summary>
public sealed partial class DmScope
{
    /// <summary>
    /// Loads relations onto rows already read, by one query per relation and batch of the rows' keys:
    /// <c>LoadAsync(orders, o =&gt; o.Include(x =&gt; x.Customer).Include(x =&gt; x.Lines, l =&gt; l.Include(y =&gt; y.Product)))</c>.
    /// </summary>
    /// <typeparam name="T">The rows' model.</typeparam>
    /// <param name="rows">The rows.</param>
    /// <param name="include">The relations to load, as for a query's <c>Include</c>.</param>
    /// <param name="reload">
    /// Whether to load a relation onto a row that has it already; by default a relation that is not null is kept.
    /// </param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    /// <exception cref="ArgumentException">The include filters, orders or takes the rows themselves.</exception>
    public async Task LoadAsync<T>(
        IEnumerable<T> rows,
        Func<DmInclude<T>, DmInclude<T>> include,
        bool reload = false,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(include);
        var root = include(new DmInclude<T>());
        if (root.Filters.Count > 0 || root.Order.Count > 0 || root.TakeCount is not null)
        {
            throw new ArgumentException(
                "LoadAsync loads relations onto the rows given: Where, OrderBy and Take choose among a relation's rows, "
                    + "inside its Include.",
                nameof(include)
            );
        }

        List<object> owners = [.. rows];
        var loader = new DmlLoader(this, tx, cancellationToken);
        foreach (var relation in root.Includes)
        {
            await loader.LoadAsync(owners, relation, reload).ConfigureAwait(false);
        }
    }
}
