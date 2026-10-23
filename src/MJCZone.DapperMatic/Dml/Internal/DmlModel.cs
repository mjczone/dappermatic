// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Reflection;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// What the DML layer needs to know about a model type: its table, its mapped properties, and its key.
/// </summary>
internal sealed class DmlModel
{
    private static readonly ConcurrentDictionary<Type, DmlModel> Models = new();

    // Relations are resolved when first used, not with the model: resolving one builds the related model, whose own
    // relations may lead back to this one.
    private readonly ConcurrentDictionary<string, Lazy<DmlRelation?>> _relations = new(StringComparer.Ordinal);
    private readonly Lazy<DmlBehaviours> _behaviours;

    private DmlModel(Type type)
    {
        Type = type;
        if (type == typeof(object) || type.IsPrimitive || type == typeof(string) || type.IsValueType)
        {
            throw new InvalidOperationException(
                $"{type.Name} is not a model type: pass the model's own type, or a row of it, such as typeof(Order)."
            );
        }

        Table = DmTableFactory.GetTable(type);
        View = DmViewFactory.GetView(type);
        SchemaName = View is null ? Table.SchemaName : View.SchemaName;
        Name = View?.ViewName ?? Table.TableName;

        // Mapped properties, in the table's column order.
        var byProperty = DmTableFactory.GetPropertyColumns(type);
        Columns =
        [
            .. Table
                .Columns.Select(column => byProperty.FirstOrDefault(kv => ReferenceEquals(kv.Value, column)))
                .Where(kv => kv.Key is not null)
                .Select(kv => new DmlColumn(type.GetProperty(kv.Key)!, kv.Value)),
        ];
        ColumnsByProperty = Columns.ToDictionary(c => c.Property.Name, StringComparer.Ordinal);

        var keyNames = Table.PrimaryKeyConstraint?.Columns.Select(c => c.ColumnName).ToList() ?? [];
        Keys = keyNames.Count > 0
            ? [.. keyNames.Select(name => Columns.FirstOrDefault(c => Same(c.Column.ColumnName, name))).OfType<DmlColumn>()]
            : [.. Columns.Where(c => c.Column.IsPrimaryKey)];

        Generated = Keys.Count == 1 && Keys[0].Column.IsAutoIncrement ? Keys[0] : null;
        _behaviours = new Lazy<DmlBehaviours>(() => DmlBehaviours.Of(this));
    }

    /// <summary>
    /// Gets the model type.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets the table the model is built into.
    /// </summary>
    public DmTable Table { get; }

    /// <summary>
    /// Gets the view the model reads from, if it is a view model (<c>[DmView]</c>), which cannot be written.
    /// </summary>
    public DmView? View { get; }

    /// <summary>
    /// Gets the schema of the model's table or view, or null for the default.
    /// </summary>
    public string? SchemaName { get; }

    /// <summary>
    /// Gets the name of the model's table or view.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the mapped properties, in the table's column order.
    /// </summary>
    public IReadOnlyList<DmlColumn> Columns { get; }

    /// <summary>
    /// Gets the mapped properties by property name.
    /// </summary>
    public IReadOnlyDictionary<string, DmlColumn> ColumnsByProperty { get; }

    /// <summary>
    /// Gets the primary key's properties, in key order; empty if the model has no primary key.
    /// </summary>
    public IReadOnlyList<DmlColumn> Keys { get; }

    /// <summary>
    /// Gets the key property whose value the database generates, if the key is a single auto-increment column.
    /// </summary>
    public DmlColumn? Generated { get; }

    /// <summary>
    /// Gets the columns the DML methods fill or filter on their own: soft delete, concurrency token, audit, tenant.
    /// </summary>
    public DmlBehaviours Behaviours => _behaviours.Value;

    /// <summary>
    /// Gets the model for a type, built once.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <returns>The model.</returns>
    public static DmlModel For(Type type) => Models.GetOrAdd(type, t => new DmlModel(t));

    /// <summary>
    /// Gets the relation a property declares with <see cref="DmRelationAttribute"/>, resolved once.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns>The relation, or null if the property is not a relation of this model.</returns>
    /// <exception cref="InvalidOperationException">The relation's keys cannot be found, or its parts do not fit.</exception>
    public DmlRelation? RelationOf(PropertyInfo property)
    {
        if (!property.DeclaringType!.IsAssignableFrom(Type))
        {
            return null;
        }

        return _relations
            .GetOrAdd(
                property.Name,
                name => new Lazy<DmlRelation?>(() =>
                    Type.GetProperty(name) is { } own && own.GetCustomAttribute<DmRelationAttribute>(inherit: true) is { } attribute
                        ? DmlRelation.Resolve(this, own, attribute)
                        : null
                )
            )
            .Value;
    }

    /// <summary>
    /// Lists the primary key and every unique constraint and unique index, as the model declares them.
    /// </summary>
    /// <returns>Each set's name, if it has one, and its columns.</returns>
    public List<(string? Name, IReadOnlyList<DmlColumn> Columns)> UniqueColumnSets()
    {
        List<DmlColumn> Resolve(IEnumerable<string> names) =>
            [.. names.Select(n => Columns.FirstOrDefault(c => string.Equals(c.Column.ColumnName, n, StringComparison.OrdinalIgnoreCase))).OfType<DmlColumn>()];

        var sets = new List<(string? Name, IReadOnlyList<DmlColumn> Columns)>();
        if (Keys.Count > 0)
        {
            sets.Add((Table.PrimaryKeyConstraint?.ConstraintName, Keys));
        }

        sets.AddRange(Table.UniqueConstraints.Select(u => ((string?)u.ConstraintName, (IReadOnlyList<DmlColumn>)Resolve(u.Columns.Select(c => c.ColumnName)))));
        sets.AddRange(Table.Indexes.Where(i => i.IsUnique).Select(i => ((string?)i.IndexName, (IReadOnlyList<DmlColumn>)Resolve(i.Columns.Select(c => c.ColumnName)))));
        sets.AddRange(Columns.Where(c => c.Column.IsUnique).Select(c => ((string?)null, (IReadOnlyList<DmlColumn>)[c])));
        return sets;
    }

    /// <summary>
    /// Throws unless the model has a primary key, for an operation that needs one.
    /// </summary>
    /// <param name="operation">The operation, for the message.</param>
    public void RequireKey(string operation)
    {
        if (Keys.Count == 0)
        {
            throw new InvalidOperationException(
                $"{operation} needs a primary key, and {Type.Name} has none: mark its key with "
                    + "[DmColumn(isPrimaryKey: true)] or [DmPrimaryKeyConstraint]."
            );
        }
    }

    /// <summary>
    /// Throws if the model is a view, for an operation that writes rows.
    /// </summary>
    /// <param name="operation">The operation, for the message.</param>
    public void RequireWritable(string operation)
    {
        if (View is not null)
        {
            throw new InvalidOperationException(
                $"{operation} writes rows, and {Type.Name} reads the view {Name}, which the DML methods only read."
            );
        }
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
