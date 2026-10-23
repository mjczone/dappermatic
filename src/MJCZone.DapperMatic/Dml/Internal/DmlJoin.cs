// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Linq.Expressions;
using Dapper;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// A query of several models' rows joined by conditions written out: what the <c>DmJoinQuery</c> types build, one
/// immutable step at a time.
/// </summary>
internal sealed class DmlJoin
{
    /// <summary>Gets the scope the query runs in.</summary>
    public required DmScope Scope { get; init; }

    /// <summary>Gets the models, in the order they are joined; the first is read from.</summary>
    public required IReadOnlyList<Type> Models { get; init; }

    /// <summary>Gets each joined model's join: <c>INNER JOIN</c> or <c>LEFT JOIN</c>, and its condition.</summary>
    public required IReadOnlyList<(string Kind, LambdaExpression On)> Joins { get; init; }

    /// <summary>Gets the conditions, each over the first models' rows, ANDed.</summary>
    public IReadOnlyList<LambdaExpression> Where { get; init; } = [];

    /// <summary>Gets the ordering keys, over the rows.</summary>
    public IReadOnlyList<(LambdaExpression Key, bool Descending)> Order { get; init; } = [];

    /// <summary>Gets the rows to skip, or null.</summary>
    public int? Skip { get; init; }

    /// <summary>Gets the most rows to return, or null.</summary>
    public int? Take { get; init; }

    /// <summary>Gets a value indicating whether the rows are distinct.</summary>
    public bool Distinct { get; init; }

    /// <summary>
    /// Joins another model.
    /// </summary>
    /// <param name="type">The model.</param>
    /// <param name="kind"><c>INNER JOIN</c> or <c>LEFT JOIN</c>.</param>
    /// <param name="on">The condition, over the rows joined so far and the new one.</param>
    /// <returns>The query.</returns>
    public DmlJoin With(Type type, string kind, LambdaExpression on)
    {
        ArgumentNullException.ThrowIfNull(on);
        return new DmlJoin
        {
            Scope = Scope,
            Models = [.. Models, type],
            Joins = [.. Joins, (kind, on)],
            Where = Where,
            Order = Order,
            Skip = Skip,
            Take = Take,
            Distinct = Distinct,
        };
    }

    /// <summary>
    /// Builds the rows' <c>SELECT</c>: the first model, each joined model, and the conditions; no select list.
    /// </summary>
    /// <returns>The query, its scope, and each model's source, in order.</returns>
    public (SqlSelect Select, SqlScope Scope, IReadOnlyList<SqlSource> Sources) Open()
    {
        var first = DmlModel.For(Models[0]);
        var dialect = DmlDialect.For(Scope.Connection);
        var from = Scope.SourceOf(first, Scope.TableOf(first.Type));
        var scope = SqlScope.Select(dialect, Scope.TableOf, from, Scope.FiltersFor, Scope.HiddenFor);
        var sources = new List<SqlSource> { from };
        for (var i = 1; i < Models.Count; i++)
        {
            var (kind, on) = Joins[i - 1];
            var source = scope.Source(DmlModel.For(Models[i]), optional: kind == "LEFT JOIN");
            sources.Add(source);

            // A join's condition is over the joined rows' own columns: a relation followed there would be joined after it.
            // The joined model's tenant and deleted rows are left out in it, so that a LEFT JOIN finds no row instead.
            var condition = DmlTranslator.Condition(on, [.. sources.Take(on.Parameters.Count)], scope, relations: false);
            List<SqlNode> parts = [condition, .. scope.Filters(source)];
            scope.Join(source, kind, parts.Count == 1 ? condition : new SqlLogical("AND", parts));
        }

        List<SqlNode> where = [.. scope.Filters(from), .. Where.Select(w => DmlTranslator.Condition(w, [.. sources.Take(w.Parameters.Count)], scope))];
        return (new SqlSelect { From = from, Joins = scope.Joins, Where = where }, scope, sources);
    }

    /// <summary>
    /// Builds the <c>SELECT</c> of whole rows: each model's columns a segment, a model joined by <c>LEFT JOIN</c>
    /// reading as null where it matched no row.
    /// </summary>
    /// <param name="read">Which models' rows to read; the others' columns are left out.</param>
    /// <param name="take">The most rows to read, or null.</param>
    /// <param name="keyOrder">Whether to order by the first model's key if the query has no order.</param>
    /// <returns>The query, its scope, the sources, and the segments of its rows.</returns>
    public (SqlSelect Select, SqlScope Scope, IReadOnlyList<SqlSource> Sources, List<DmlSegment> Segments) Rows(
        IReadOnlyList<bool> read,
        int? take,
        bool keyOrder
    )
    {
        var (select, scope, sources) = Open();
        var items = new List<SqlSelectItem>();
        var segments = new List<DmlSegment>();
        for (var i = 0; i < sources.Count; i++)
        {
            var model = sources[i].Model!;
            if (!read[i])
            {
                segments.Add(new DmlSegment { Model = model, Start = items.Count, Length = 0, Collections = [] });
                continue;
            }

            // A segment of a row that may be missing reads as null when its first column is NULL: one that is not.
            var first = model.Keys.Count > 0 ? model.Keys[0] : model.Columns.FirstOrDefault(c => !c.Column.IsNullable) ?? model.Columns[0];
            List<DmlColumn> columns = [first, .. model.Columns.Where(c => c != first)];
            segments.Add(
                new DmlSegment
                {
                    Model = model,
                    Start = items.Count,
                    Length = columns.Count,
                    Nullable = i > 0 && Joins[i - 1].Kind == "LEFT JOIN",
                    Collections = [],
                }
            );
            items.AddRange(columns.Select(c => new SqlSelectItem(new SqlColumn(sources[i], c.Column.ColumnName), c.Property.Name)));
        }

        List<SqlOrder> order =
        [
            .. Order.Select(o =>
            {
                var (node, nullable) = DmlTranslator.Value(o.Key, [.. sources.Take(o.Key.Parameters.Count)], scope);
                return new SqlOrder(node, o.Descending, nullable);
            }),
        ];
        if (order.Count == 0 && keyOrder)
        {
            order = [.. sources[0].Model!.Keys.Select(k => new SqlOrder(new SqlColumn(sources[0], k.Column.ColumnName), false, false))];
        }

        var paged = Skip is not null || take is not null;
        var orderBySql = order.Count == 0 && Distinct && paged && scope.Dialect.ProviderType == DbProviderType.SqlServer ? "1" : null;
        return (
            select with
            {
                Items = items,
                Distinct = Distinct,
                OrderBy = order,
                OrderBySql = orderBySql,
                Skip = Skip,
                Take = take,
            },
            scope,
            sources,
            segments
        );
    }

    /// <summary>
    /// Reads the rows of a query built by <see cref="Rows"/>, with values of its own after the segments.
    /// </summary>
    /// <param name="select">The query.</param>
    /// <param name="dialect">The dialect.</param>
    /// <param name="segments">The segments of its rows.</param>
    /// <param name="valueCount">The number of values after the segments.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Each row's segments' objects, then its values.</returns>
    public Task<List<object?[]>> ReadAsync(
        SqlSelect select,
        DmlDialect dialect,
        List<DmlSegment> segments,
        int valueCount,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        var parameters = new DmlParameters();
        var sql = new SqlRenderer(dialect, parameters).For(select).Render(select);
        return new DmlLoader(Scope, tx, cancellationToken).ReadAllAsync(sql, parameters, segments, select.Items.Count - valueCount, valueCount);
    }

    /// <summary>
    /// Counts the rows, or tells whether there are any.
    /// </summary>
    /// <param name="exists">Whether to tell whether there are any rather than count them.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows, or 1 or 0.</returns>
    public async Task<long> CountAsync(bool exists, IDbTransaction? tx, CancellationToken cancellationToken)
    {
        if (Take == 0)
        {
            return 0;
        }

        var (rows, scope, _, _) = Rows([.. Models.Select(_ => true)], Take, keyOrder: false);

        // A paged or distinct query is counted in a subquery, whose columns are named apart: models share column names.
        var paged = Distinct || Skip is not null || Take is not null;
        // SQL Server refuses ORDER BY in a subquery that is not paged.
        var inner = paged
            ? rows with
            {
                Items = [.. rows.Items.Select((item, i) => item with { Alias = $"c{i}" })],
                OrderBy = Skip is null && Take is null ? [] : rows.OrderBy,
            }
            : rows with { Items = [], OrderBy = [] };
        var parameters = new DmlParameters();
        string sql;
        if (exists)
        {
            var test = new SqlExists(inner);
            sql = $"SELECT CASE WHEN {new SqlRenderer(scope.Dialect, parameters).For(test).Render(test)} THEN 1 ELSE 0 END";
        }
        else
        {
            var count = new SqlSelectItem(new SqlAggregate("COUNT", null), null);
            var select = paged ? new SqlSelect { From = rows.From, FromSubquery = inner, Items = [count] } : inner with { Items = [count] };
            sql = new SqlRenderer(scope.Dialect, parameters).For(select).Render(select);
        }

        return await Scope.Connection.ExecuteScalarAsync<long>(DmScope.Command(sql, parameters, tx, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// The rows to group, for <c>GroupBy</c>.
    /// </summary>
    /// <returns>The rows.</returns>
    /// <exception cref="InvalidOperationException">The query is ordered, paged or distinct.</exception>
    public DmlRows Grouped() =>
        Order.Count > 0 || Skip is not null || Take is not null || Distinct
            ? throw new InvalidOperationException("GroupBy follows Join and Where: order and page the groups after it.")
            : new DmlRows { Scope = Scope, Open = Open };
}
