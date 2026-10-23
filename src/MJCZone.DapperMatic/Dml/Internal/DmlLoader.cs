// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Data;
using System.Data.Common;
using System.Reflection;
using Dapper;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// Loads rows with their included relations: a relation of one row is joined into the query that reads its owners,
/// and a collection is read by one query per batch of owners' keys, after its owners.
/// </summary>
/// <remarks>
/// A loaded collection is never null: an owner with no related rows gets an empty one. A relation of one row is null
/// when there is no related row, as it is when it was not loaded.
/// </remarks>
internal sealed class DmlLoader
{
    // Owners' keys per collection query: well within every provider's limit on parameters, composite keys included.
    private const int KeysPerQuery = 1_000;

    private readonly DmScope _scope;
    private readonly DmlDialect _dialect;
    private readonly IDbTransaction? _tx;
    private readonly CancellationToken _cancellationToken;
    private bool? _windows;

    /// <summary>
    /// Initializes a new instance of the <see cref="DmlLoader"/> class.
    /// </summary>
    /// <param name="scope">The scope the rows are read in.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public DmlLoader(DmScope scope, IDbTransaction? tx, CancellationToken cancellationToken)
    {
        _scope = scope;
        _dialect = DmlDialect.For(scope.Connection);
        _tx = tx;
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    /// Adds the columns of the relations of one row a query's includes reach to its select list, joining them.
    /// </summary>
    /// <param name="select">The query, reading its model's columns.</param>
    /// <param name="scope">The query's scope, which joins the related rows.</param>
    /// <param name="includes">The includes of the query's rows.</param>
    /// <returns>The query reading the related rows too, and the segments of its rows.</returns>
    public static (SqlSelect Select, List<DmlSegment> Segments) Plan(SqlSelect select, SqlScope scope, IReadOnlyList<DmlInclude> includes)
    {
        var items = new List<SqlSelectItem>(select.Items);
        var segments = new List<DmlSegment>
        {
            new() { Model = select.From.Model!, Start = 0, Length = items.Count, Collections = Collections(includes) },
        };
        AddJoined(scope, select.From, 0, includes, items, segments);
        return (select with { Items = items }, segments);
    }

    /// <summary>
    /// Runs a query planned by <see cref="Plan"/>, then loads its rows' collections.
    /// </summary>
    /// <typeparam name="T">The query's model.</typeparam>
    /// <param name="sql">The query's SQL.</param>
    /// <param name="parameters">Its parameters.</param>
    /// <param name="segments">The segments of its rows.</param>
    /// <returns>The rows.</returns>
    public async Task<List<T>> ReadAsync<T>(string sql, DmlParameters parameters, List<DmlSegment> segments)
    {
        var rows = await ReadRowsAsync(sql, parameters, segments).ConfigureAwait(false);
        await LoadCollectionsAsync(segments).ConfigureAwait(false);
        return rows.ConvertAll(row => (T)row[0]!);
    }

    /// <summary>
    /// Runs a query planned by <see cref="Plan"/> whose select list ends with values of its own, after the segments,
    /// then loads its rows' collections.
    /// </summary>
    /// <param name="sql">The query's SQL.</param>
    /// <param name="parameters">Its parameters.</param>
    /// <param name="segments">The segments of its rows.</param>
    /// <param name="valuesStart">The index of the first value.</param>
    /// <param name="valueCount">The number of values.</param>
    /// <returns>Each row, null if its segment reads no column, and its values.</returns>
    public async Task<List<(object? Row, object?[] Values)>> ReadWithValuesAsync(
        string sql,
        DmlParameters parameters,
        List<DmlSegment> segments,
        int valuesStart,
        int valueCount
    )
    {
        var rows = await ReadRowsAsync(sql, parameters, segments, valuesStart, valueCount).ConfigureAwait(false);
        await LoadCollectionsAsync(segments).ConfigureAwait(false);
        return rows.ConvertAll(row => (row[0], row[segments.Count..]));
    }

    /// <summary>
    /// Runs a query of several segments and values of its own, loading no collections.
    /// </summary>
    /// <param name="sql">The query's SQL.</param>
    /// <param name="parameters">Its parameters.</param>
    /// <param name="segments">The segments of its rows.</param>
    /// <param name="valuesStart">The index of the first value.</param>
    /// <param name="valueCount">The number of values.</param>
    /// <returns>Each row's segments' objects, then its values.</returns>
    public Task<List<object?[]>> ReadAllAsync(string sql, DmlParameters parameters, List<DmlSegment> segments, int valuesStart, int valueCount) =>
        ReadRowsAsync(sql, parameters, segments, valuesStart, valueCount);

    /// <summary>
    /// Loads a relation onto rows already read, by one query per batch of their keys.
    /// </summary>
    /// <param name="owners">The rows.</param>
    /// <param name="include">The relation, which rows of it, and what with them.</param>
    /// <param name="reload">Whether to load the relation onto rows that have it already (not null).</param>
    /// <returns>A task.</returns>
    public async Task LoadAsync(IReadOnlyList<object> owners, DmlInclude include, bool reload)
    {
        var relation = include.Relation;
        var setter = relation.Property;
        if (!setter.CanWrite)
        {
            throw new InvalidOperationException($"{relation.Owner.Type.Name}.{setter.Name} has no setter, so it cannot be loaded.");
        }

        var ownerKeys = relation.Link is null ? relation.Keys.Select(k => k.This).ToList() : relation.LinkKeys.Select(k => k.This).ToList();
        var byKey = new Dictionary<DmlKey, List<object>>();
        foreach (var owner in owners)
        {
            if (!reload && setter.GetValue(owner) is not null)
            {
                continue;
            }

            // An owner the relation does not apply to, or with no key, has no related rows.
            var key = DmlKey.Of(owner, ownerKeys);
            if (key.HasNull || relation.ThisValues.Any(v => !Equals(v.Column.Property.GetValue(owner), v.Value)))
            {
                setter.SetValue(owner, relation.IsCollection ? Collection(relation.Property, relation.OtherType, []) : null);
                continue;
            }

            if (!byKey.TryGetValue(key, out var same))
            {
                byKey[key] = same = [];
            }

            same.Add(owner);
        }

        if (byKey.Count == 0)
        {
            return;
        }

        var related = new Dictionary<DmlKey, List<object>>();
        var keys = byKey.Keys.ToList();
        var perQuery = Math.Max(1, KeysPerQuery / ownerKeys.Count);
        List<DmlSegment>? segments = null;
        for (var i = 0; i < keys.Count; i += perQuery)
        {
            segments = await ReadRelatedAsync(include, keys.GetRange(i, Math.Min(perQuery, keys.Count - i)), related, segments)
                .ConfigureAwait(false);
        }

        foreach (var (key, same) in byKey)
        {
            var rows = related.TryGetValue(key, out var found) ? found : [];
            if (!relation.IsCollection && rows.Count > 1)
            {
                throw new InvalidOperationException(
                    $"{relation.Owner.Type.Name}.{setter.Name} is one row, and {rows.Count} {relation.OtherType.Name} rows match "
                        + "one of the rows' keys."
                );
            }

            foreach (var owner in same)
            {
                setter.SetValue(owner, relation.IsCollection ? Collection(relation.Property, relation.OtherType, rows) : rows.FirstOrDefault());
            }
        }

        if (segments is not null)
        {
            await LoadCollectionsAsync(segments).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Makes a collection of a property's type holding rows: an array, a <see cref="List{T}"/> for any type a list
    /// fits, or the property's own type, filled with its <c>Add</c>.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="elementType">The rows' type.</param>
    /// <param name="rows">The rows.</param>
    /// <returns>The collection.</returns>
    /// <exception cref="NotSupportedException">No collection of the property's type can be made.</exception>
    internal static object Collection(PropertyInfo property, Type elementType, List<object> rows)
    {
        var type = property.PropertyType;
        if (type.IsArray)
        {
            var array = Array.CreateInstance(elementType, rows.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                array.SetValue(rows[i], i);
            }

            return array;
        }

        var listType = typeof(List<>).MakeGenericType(elementType);
        var collectionType = type.IsAssignableFrom(listType) ? listType
            : !type.IsAbstract && !type.IsInterface && type.GetConstructor(Type.EmptyTypes) is not null ? type
            : throw new NotSupportedException(
                $"{property.DeclaringType!.Name}.{property.Name} is a {type.Name}, which a List<{elementType.Name}> "
                    + "does not fit and which cannot be made: declare it as a List, an array, or an interface a List implements."
            );
        var collection = Activator.CreateInstance(collectionType)!;
        if (collection is IList list)
        {
            foreach (var row in rows)
            {
                list.Add(row);
            }

            return collection;
        }

        var add = collectionType.GetMethod("Add", [elementType])
            ?? throw new NotSupportedException($"{type.Name} has no Add({elementType.Name}) to fill it with.");
        foreach (var row in rows)
        {
            add.Invoke(collection, [row]);
        }

        return collection;
    }

    /// <summary>
    /// Keeps at most a number of rows per owner, by the model's key columns.
    /// </summary>
    /// <param name="select">The query.</param>
    /// <param name="from">Its source.</param>
    /// <param name="rowKeys">The columns of the owners' keys.</param>
    /// <param name="order">The order within an owner's rows.</param>
    /// <param name="take">The most rows per owner.</param>
    /// <returns>The query of each owner's first rows.</returns>
    internal static SqlSelect PerOwner(SqlSelect select, SqlSource from, List<DmlColumn> rowKeys, List<SqlOrder> order, int take) =>
        PerOwner(select, from, [.. rowKeys.Select(k => k.Column.ColumnName)], order, take);

    /// <summary>
    /// Keeps at most a number of rows per owner: numbered within each owner's rows in an order, in a subquery whose
    /// columns are named c0, c1, ..., since segments repeat column names, which a derived table refuses.
    /// </summary>
    /// <param name="select">The query.</param>
    /// <param name="from">Its source.</param>
    /// <param name="rowKeys">The columns of the owners' keys.</param>
    /// <param name="order">The order within an owner's rows.</param>
    /// <param name="take">The most rows per owner.</param>
    /// <returns>The query of each owner's first rows.</returns>
    internal static SqlSelect PerOwner(SqlSelect select, SqlSource from, List<string> rowKeys, List<SqlOrder> order, int take)
    {
        var rowNumber = new SqlRowNumber([.. rowKeys.Select(k => (SqlNode)new SqlColumn(from, k))], order);
        var inner = select with
        {
            Items = [.. select.Items.Select((item, i) => item with { Alias = $"c{i}" }), new SqlSelectItem(rowNumber, "rn")],
            OrderBy = [],
        };
        return new SqlSelect
        {
            From = from,
            FromSubquery = inner,
            Items = [.. select.Items.Select((item, i) => new SqlSelectItem(new SqlColumn(from, $"c{i}"), item.Alias))],
            Where = [new SqlCompare(new SqlColumn(from, "rn"), "<=", new SqlValue(typeof(int), take))],
            OrderBy = [new SqlOrder(new SqlColumn(from, "rn"), Descending: false, Nullable: false)],
        };
    }

    private static List<DmlInclude> Collections(IReadOnlyList<DmlInclude> includes) => [.. includes.Where(i => i.Relation.IsCollection)];

    // The joined relations of one row, to any depth, each a segment of the row.
    private static void AddJoined(
        SqlScope scope,
        SqlSource owner,
        int ownerSegment,
        IReadOnlyList<DmlInclude> includes,
        List<SqlSelectItem> items,
        List<DmlSegment> segments
    )
    {
        foreach (var include in includes.Where(i => !i.Relation.IsCollection))
        {
            var joined = DmlTranslator.Join(scope, owner, include.Relation)
                ?? throw new InvalidOperationException("A query that loads related rows joins them.");
            var model = include.Relation.Other;

            // The segment reads as null when its first column is NULL: one that is never NULL in a related row.
            var first = model.Keys.Count > 0 ? model.Keys[0] : include.Relation.Keys[0].Other;
            List<DmlColumn> columns = [first, .. model.Columns.Where(c => c != first)];
            segments.Add(
                new DmlSegment
                {
                    Model = model,
                    Start = items.Count,
                    Length = columns.Count,
                    Parent = ownerSegment,
                    Relation = include.Relation,
                    Nullable = true,
                    Collections = Collections(include.Includes),
                }
            );
            items.AddRange(columns.Select(c => new SqlSelectItem(new SqlColumn(joined, c.Column.ColumnName), c.Property.Name)));
            AddJoined(scope, joined, segments.Count - 1, include.Includes, items, segments);
        }
    }

    // Owners' keys in one IN list, or (a = @p AND b = @p) OR ... for a key of several columns.
    private static SqlNode KeyIn(SqlSource source, IReadOnlyList<DmlColumn> columns, List<DmlKey> keys)
    {
        if (columns.Count == 1)
        {
            var type = columns[0].Property.PropertyType;
            return new SqlIn(new SqlColumn(source, columns[0].Column.ColumnName), [.. keys.Select(k => new SqlValue(type, k.Values[0]))], Negated: false);
        }

        return new SqlLogical(
            "OR",
            [
                .. keys.Select(k => (SqlNode)new SqlLogical(
                    "AND",
                    [
                        .. columns.Select((c, i) =>
                            (SqlNode)new SqlCompare(new SqlColumn(source, c.Column.ColumnName), "=", new SqlValue(c.Property.PropertyType, k.Values[i]))
                        ),
                    ]
                )),
            ]
        );
    }

    private static SqlNode AllOf(List<SqlNode> parts) => parts.Count == 1 ? parts[0] : new SqlLogical("AND", parts);

    // One batch of a relation's rows, added to the rows found by key. The segments of the first batch are reused, so
    // that the rows of every batch are loaded together.
    private async Task<List<DmlSegment>> ReadRelatedAsync(
        DmlInclude include,
        List<DmlKey> keys,
        Dictionary<DmlKey, List<object>> related,
        List<DmlSegment>? segments
    )
    {
        var relation = include.Relation;
        var other = relation.Other;
        var fromModel = relation.Link ?? other;
        var from = _scope.SourceOf(fromModel, _scope.TableOf(fromModel.Type));

        // Related rows are never the rows a query is about: OnlyDeleted reads the owners' deleted rows, not theirs.
        var scope = SqlScope.Select(_dialect, _scope.TableOf, from, (source, _) => _scope.FiltersFor(source, root: false), _scope.HiddenFor);

        // Through a link model, its rows are read with the related rows, for their keys to the owners.
        var rows = from;
        if (relation.Link is not null)
        {
            rows = scope.Source(other);
            scope.Join(
                rows,
                "INNER JOIN",
                AllOf([.. relation.Keys.Select(k => (SqlNode)new SqlCompare(new SqlColumn(rows, k.Other.Column.ColumnName), "=", new SqlColumn(from, k.This.Column.ColumnName)))])
            );
        }

        var rowKeys = relation.Link is null ? relation.Keys.Select(k => k.Other).ToList() : relation.LinkKeys.Select(k => k.Link).ToList();
        List<SqlNode> where =
        [
            KeyIn(from, rowKeys, keys),
            .. DmlTranslator.FixedValues(rows, relation.OtherValues),
            .. scope.Filters(from),
            .. relation.Link is null ? [] : scope.Filters(rows),
        ];
        if (relation.Condition is { } condition)
        {
            // A condition over both rows needs the owner's: joined by the keys, which the batch already matched.
            var owner = rows;
            if (condition.Parameters.Count == 2)
            {
                owner = scope.Source(relation.Owner);
                var ownerKeys = relation.Link is null ? relation.Keys.Select(k => k.This).ToList() : relation.LinkKeys.Select(k => k.This).ToList();
                scope.Join(
                    owner,
                    "INNER JOIN",
                    AllOf([.. ownerKeys.Select((k, i) => (SqlNode)new SqlCompare(new SqlColumn(owner, k.Column.ColumnName), "=", new SqlColumn(from, rowKeys[i].Column.ColumnName)))])
                );
            }

            where.Add(DmlTranslator.ConditionOver(scope, condition, owner, rows));
        }

        where.AddRange(include.Filters.Select(f => DmlTranslator.Condition(f, rows, scope)));

        // The related rows, the link rows, then the relations of one row the include joins.
        var items = new List<SqlSelectItem>(DmScope.Items(rows));
        var plan = new List<DmlSegment>
        {
            new() { Model = other, Start = 0, Length = items.Count, Collections = Collections(include.Includes) },
        };
        if (relation.Link is { } link)
        {
            plan.Add(new DmlSegment { Model = link, Start = items.Count, Length = link.Columns.Count, Parent = -1, Collections = [] });
            items.AddRange(DmScope.Items(from));
        }

        AddJoined(scope, rows, 0, include.Includes, items, plan);

        List<SqlOrder> order =
        [
            .. include.Order.Select(o =>
            {
                var (node, nullable, _) = DmlTranslator.Value(o.Key, rows, scope);
                return new SqlOrder(node, o.Descending, nullable);
            }),
        ];
        if (order.Count == 0 && include.Take is not null)
        {
            // Take keeps the first rows of an order: key order, if the include gives none.
            order = [.. (other.Keys.Count > 0 ? other.Keys : other.Columns.Take(1)).Select(k => new SqlOrder(new SqlColumn(rows, k.Column.ColumnName), false, false))];
        }

        var select = new SqlSelect { From = from, Joins = scope.Joins, Items = items, Where = where, OrderBy = order };
        var trimInMemory = false;
        if (include.Take is { } take)
        {
            if (await SupportsWindowsAsync().ConfigureAwait(false))
            {
                select = PerOwner(select, from, rowKeys, order, take);
            }
            else
            {
                trimInMemory = true;
            }
        }

        var parameters = new DmlParameters();
        var sql = new SqlRenderer(_dialect, parameters).For(select).Render(select);
        var keyOwner = relation.Link is null ? 0 : 1;
        segments ??= plan;
        var read = await ReadRowsAsync(sql, parameters, segments).ConfigureAwait(false);
        foreach (var row in read)
        {
            var key = DmlKey.Of(row[keyOwner]!, rowKeys);
            if (!related.TryGetValue(key, out var found))
            {
                related[key] = found = [];
            }

            if (!trimInMemory || found.Count < include.Take)
            {
                found.Add(row[0]!);
            }
        }

        return segments;
    }

    // Reads the rows of a query, each segment into an object, and sets each joined object on its owner.
    private async Task<List<object?[]>> ReadRowsAsync(
        string sql,
        DmlParameters parameters,
        List<DmlSegment> segments,
        int valuesStart = 0,
        int valueCount = 0
    )
    {
        var command = DmScope.Command(sql, parameters, _tx, _cancellationToken);
        using var reader = await _scope.Connection.ExecuteReaderAsync(command).ConfigureAwait(false);
        var parsers = segments
            .Select(s => s.Length == 0 ? null : reader.GetRowParser(s.Model.Type, s.Start, s.Length, returnNullIfFirstMissing: s.Nullable))
            .ToArray();

        var rows = new List<object?[]>();
        while (reader is DbDataReader db ? await db.ReadAsync(_cancellationToken).ConfigureAwait(false) : reader.Read())
        {
            var row = new object?[segments.Count + valueCount];
            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                if ((segment.Parent >= 0 && row[segment.Parent] is null) || parsers[i] is not { } parse)
                {
                    continue;
                }

                row[i] = parse(reader);
                if (row[i] is { } value)
                {
                    segment.Rows.Add(value);
                    if (segment.Parent >= 0)
                    {
                        segment.Relation!.Property.SetValue(row[segment.Parent], value);
                    }
                }
            }

            for (var j = 0; j < valueCount; j++)
            {
                row[segments.Count + j] = reader.IsDBNull(valuesStart + j) ? null : reader.GetValue(valuesStart + j);
            }

            rows.Add(row);
        }

        return rows;
    }

    private async Task LoadCollectionsAsync(List<DmlSegment> segments)
    {
        foreach (var segment in segments)
        {
            foreach (var include in segment.Collections)
            {
                await LoadAsync(segment.Rows, include, reload: true).ConfigureAwait(false);
            }
        }
    }

    // ROW_NUMBER() is in every provider and version tested except MySQL before 8.0 (MariaDB has it from 10.2).
    private async Task<bool> SupportsWindowsAsync()
    {
        if (_windows is { } known)
        {
            return known;
        }

        if (_dialect.ProviderType != DbProviderType.MySql)
        {
            return (_windows = true).Value;
        }

        var version = await _scope.Connection.GetDatabaseVersionAsync(_tx, _cancellationToken).ConfigureAwait(false);
        return (_windows = version.Major >= 8).Value;
    }
}

/// <summary>
/// The columns of a result row one model's object is read from, and where the object goes.
/// </summary>
internal sealed class DmlSegment
{
    /// <summary>Gets the model.</summary>
    public required DmlModel Model { get; init; }

    /// <summary>Gets the index of the segment's first column.</summary>
    public required int Start { get; init; }

    /// <summary>Gets the number of the segment's columns.</summary>
    public required int Length { get; init; }

    /// <summary>Gets the segment of the object this one is a relation of, or -1.</summary>
    public int Parent { get; init; } = -1;

    /// <summary>Gets the relation of the parent's object this object is set on, or null.</summary>
    public DmlRelation? Relation { get; init; }

    /// <summary>Gets a value indicating whether the object is null when the segment's first column is NULL.</summary>
    public bool Nullable { get; init; }

    /// <summary>Gets the collections to load onto the segment's objects once they are read.</summary>
    public required IReadOnlyList<DmlInclude> Collections { get; init; }

    /// <summary>Gets the objects read.</summary>
    public List<object> Rows { get; } = [];
}

/// <summary>
/// A row's key values, compared as they are typed: never as strings, never by position in a result.
/// </summary>
internal readonly struct DmlKey : IEquatable<DmlKey>
{
    private DmlKey(object?[] values) => Values = values;

    /// <summary>Gets the key's values.</summary>
    public object?[] Values { get; }

    /// <summary>Gets a value indicating whether a value of the key is null, which matches nothing.</summary>
    public bool HasNull => Values.Any(v => v is null);

    /// <summary>
    /// Reads a row's key.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="columns">The key's properties.</param>
    /// <returns>The key.</returns>
    public static DmlKey Of(object row, IReadOnlyList<DmlColumn> columns) =>
        new([.. columns.Select(c => c.Property.GetValue(row))]);

    /// <inheritdoc />
    public bool Equals(DmlKey other) => Values.AsSpan().SequenceEqual(other.Values);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DmlKey other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var value in Values)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }
}
