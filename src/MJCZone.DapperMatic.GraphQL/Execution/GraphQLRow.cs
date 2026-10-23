// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Dml.Internal;
using MJCZone.DapperMatic.Dml.Rest;
using MJCZone.DapperMatic.GraphQL.Schema;

namespace MJCZone.DapperMatic.GraphQL.Execution;

/// <summary>
/// A row as GraphQL resolves it: its values as the engine read them, and the table it is of, which gives a
/// <c>Node</c> its type.
/// </summary>
/// <param name="table">The table.</param>
/// <param name="values">The values: columns by name, related rows under the field's response key.</param>
internal sealed class GraphQLRow(GraphQLTable table, IReadOnlyDictionary<string, object?> values)
{
    /// <summary>Gets the table.</summary>
    public GraphQLTable Table { get; } = table;

    /// <summary>Gets the row's values.</summary>
    public IReadOnlyDictionary<string, object?> Values { get; } = values;

    /// <summary>
    /// Gets a column's value, read as its column's type.
    /// </summary>
    /// <param name="column">The column.</param>
    /// <returns>The value.</returns>
    public object? Column(RestColumn column)
    {
        var value = Find(column.Name);
        if (value is null or DBNull)
        {
            return null;
        }

        try
        {
            return DmlValues.Convert(value, column.Type);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            // A value with no .NET type of its own (JSON text, a range, a shape) is written as it came.
            return value;
        }
    }

    /// <summary>
    /// Gets the related row or rows read under a field's response key.
    /// </summary>
    /// <param name="key">The response key: the field's alias, or its name.</param>
    /// <param name="other">The related table.</param>
    /// <param name="many">Whether the relation is to many rows.</param>
    /// <returns>The related row, a list of them, or null.</returns>
    public object? Related(string key, GraphQLTable other, bool many)
    {
        var value = Find(key);
        if (many)
        {
            return value is IEnumerable<Dictionary<string, object?>> rows ? rows.Select(r => new GraphQLRow(other, r)).ToList() : [];
        }

        return value is IReadOnlyDictionary<string, object?> row ? new GraphQLRow(other, row) : null;
    }

    /// <summary>Gets the row's global id, from its table and key.</summary>
    /// <returns>The id.</returns>
    public string NodeId() =>
        GraphQLNodeIds.Encode(Table.Table.Name, Table.Table.Keys.Select(k => Column(Table.Table.Column(k, "the key"))));

    /// <summary>Gets a value by name, whatever its case.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The value, or null if the row has none.</returns>
    public object? Find(string name)
    {
        if (Values.TryGetValue(name, out var value))
        {
            return value;
        }

        foreach (var (key, found) in Values)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return found;
            }
        }

        return null;
    }
}
