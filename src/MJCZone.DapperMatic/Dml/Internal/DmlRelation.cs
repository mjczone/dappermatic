// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using MJCZone.DapperMatic.DataAnnotations;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// A relation property of a model, resolved: what it joins to and on what.
/// </summary>
internal sealed class DmlRelation
{
    private DmlRelation(PropertyInfo property, DmlModel owner, Type otherType, bool isCollection)
    {
        Property = property;
        Owner = owner;
        OtherType = otherType;
        IsCollection = isCollection;
    }

    /// <summary>Gets the relation property.</summary>
    public PropertyInfo Property { get; }

    /// <summary>Gets the model the property belongs to.</summary>
    public DmlModel Owner { get; }

    /// <summary>Gets the related model's type: the property's type, or its element type for a collection.</summary>
    public Type OtherType { get; }

    /// <summary>Gets the related model.</summary>
    public DmlModel Other => DmlModel.For(OtherType);

    /// <summary>Gets a value indicating whether the property holds many related rows.</summary>
    public bool IsCollection { get; }

    /// <summary>
    /// Gets the key pairs joining the owner (or, through a link, the link) to the related model: each pair's first
    /// column is the owner's (or the link's), its second the related model's.
    /// </summary>
    public IReadOnlyList<(DmlColumn This, DmlColumn Other)> Keys { get; private set; } = [];

    /// <summary>Gets the link model of a many-to-many relation, or null.</summary>
    public DmlModel? Link { get; private set; }

    /// <summary>Gets the key pairs joining the owner to the link: the owner's column, then the link's.</summary>
    public IReadOnlyList<(DmlColumn This, DmlColumn Link)> LinkKeys { get; private set; } = [];

    /// <summary>Gets the fixed values the related rows have.</summary>
    public IReadOnlyList<(DmlColumn Column, object? Value)> OtherValues { get; private set; } = [];

    /// <summary>Gets the fixed values the owner's rows must have for the relation to apply.</summary>
    public IReadOnlyList<(DmlColumn Column, object? Value)> ThisValues { get; private set; } = [];

    /// <summary>
    /// Gets the condition the related rows must match: over the related row, or over the owner's row and the related
    /// row.
    /// </summary>
    public LambdaExpression? Condition { get; private set; }

    /// <summary>
    /// Resolves a relation property.
    /// </summary>
    /// <param name="owner">The model the property belongs to.</param>
    /// <param name="property">The property.</param>
    /// <param name="attribute">Its <see cref="DmRelationAttribute"/>.</param>
    /// <returns>The relation.</returns>
    /// <exception cref="InvalidOperationException">The relation's keys cannot be found, or its parts do not fit.</exception>
    public static DmlRelation Resolve(DmlModel owner, PropertyInfo property, DmRelationAttribute attribute)
    {
        var (otherType, isCollection) = ElementOf(property.PropertyType);
        var relation = new DmlRelation(property, owner, otherType, isCollection);
        var other = relation.Other;
        string Where() => $"{owner.Type.Name}.{property.Name}";

        if (attribute.Through is { } through)
        {
            if (!isCollection)
            {
                throw new InvalidOperationException($"{Where()} goes through {through.Name}, so it must be a collection.");
            }

            var link = DmlModel.For(through);
            relation.Link = link;
            relation.LinkKeys = [.. ForeignKey(link, owner, Where).Select(p => (This: p.Referenced, Link: p.Source))];
            relation.Keys = [.. ForeignKey(link, other, Where).Select(p => (This: p.Source, Other: p.Referenced))];
        }
        else if (attribute.ThisKeys is not null || attribute.OtherKeys is not null)
        {
            var thisKeys = attribute.ThisKeys is null ? Primary(owner, Where) : Columns(owner, attribute.ThisKeys, Where);
            var otherKeys = attribute.OtherKeys is null ? Primary(other, Where) : Columns(other, attribute.OtherKeys, Where);
            if (thisKeys.Count != otherKeys.Count)
            {
                throw new InvalidOperationException($"{Where()} names {thisKeys.Count} keys on one side and {otherKeys.Count} on the other.");
            }

            relation.Keys = [.. thisKeys.Zip(otherKeys)];
        }
        else if (attribute.ForeignKey is { } foreignKey)
        {
            relation.Keys = isCollection
                ? [.. Primary(owner, Where).Zip(Columns(other, [foreignKey], Where))]
                : [.. Columns(owner, [foreignKey], Where).Zip(Primary(other, Where))];
        }
        else
        {
            // One related row: this model's foreign key to it, else its foreign key to this model (one-to-one).
            // Many: their foreign key to this model.
            relation.Keys = isCollection
                ? [.. ForeignKey(other, owner, Where).Select(p => (This: p.Referenced, Other: p.Source))]
                : ForeignKeys(owner, other) is { Count: > 0 }
                    ? [.. ForeignKey(owner, other, Where).Select(p => (This: p.Source, Other: p.Referenced))]
                    : [.. ForeignKey(other, owner, Where).Select(p => (This: p.Referenced, Other: p.Source))];
        }

        relation.OtherValues = Values(other, attribute.OtherValues, Where);
        relation.ThisValues = Values(owner, attribute.ThisValues, Where);
        relation.Condition = attribute.Condition is null ? null : ConditionOf(owner, otherType, attribute.Condition, Where);

        foreach (var (a, b) in relation.Keys.Concat(relation.LinkKeys.Select(k => (k.This, Other: k.Link))))
        {
            if (Underlying(a) != Underlying(b))
            {
                throw new InvalidOperationException(
                    $"{Where()} joins {a.Property.DeclaringType!.Name}.{a.Property.Name} ({Underlying(a).Name}) to "
                        + $"{b.Property.DeclaringType!.Name}.{b.Property.Name} ({Underlying(b).Name}): keys are compared as "
                        + "they are typed, so they must be of one type."
                );
            }
        }

        // One related row is joined, which repeats the row it is joined to if the keys match several.
        if (!isCollection && other.View is null)
        {
            HashSet<DmlColumn> matched = [.. relation.Keys.Select(k => k.Other), .. relation.OtherValues.Select(v => v.Column)];
            if (!other.UniqueColumnSets().Any(u => u.Columns.Count > 0 && u.Columns.All(matched.Contains)))
            {
                throw new InvalidOperationException(
                    $"{Where()} is one {other.Type.Name}, and {other.Type.Name} declares no primary key, unique "
                        + $"constraint or unique index within ({string.Join(", ", matched.Select(c => c.Property.Name))}), "
                        + "so several rows could match: declare the unique key, or make the property a collection."
                );
            }
        }

        return relation;
    }

    /// <summary>
    /// Finds a property type's element type, if it is a collection of rows.
    /// </summary>
    /// <param name="type">The property type.</param>
    /// <returns>The related model type, and whether the property is a collection.</returns>
    public static (Type Type, bool IsCollection) ElementOf(Type type)
    {
        if (type == typeof(string))
        {
            return (type, false);
        }

        if (type.IsArray)
        {
            return (type.GetElementType()!, true);
        }

        var enumerable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable is null ? (type, false) : (enumerable.GetGenericArguments()[0], true);
    }

    private static Type Underlying(DmlColumn column) =>
        Nullable.GetUnderlyingType(column.Property.PropertyType) ?? column.Property.PropertyType;

    private static List<DmlColumn> Primary(DmlModel model, Func<string> where) =>
        model.Keys.Count > 0
            ? [.. model.Keys]
            : throw new InvalidOperationException($"{where()} joins on {model.Type.Name}'s primary key, and it has none: name the keys.");

    private static List<DmlColumn> Columns(DmlModel model, IEnumerable<string> names, Func<string> where) =>
        [
            .. names.Select(name =>
                model.ColumnsByProperty.TryGetValue(name, out var column)
                    ? column
                    : throw new InvalidOperationException($"{where()} names {name}, which is not a column property of {model.Type.Name}.")
            ),
        ];

    // Every foreign key a model declares to another's table: table constraints and column-level foreign keys.
    private static List<List<(DmlColumn Source, DmlColumn Referenced)>> ForeignKeys(DmlModel from, DmlModel to)
    {
        bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        DmlColumn? In(DmlModel model, string name) => model.Columns.FirstOrDefault(c => Same(c.Column.ColumnName, name));

        var keys = from.Table.ForeignKeyConstraints
            .Where(fk => Same(fk.ReferencedTableName, to.Table.TableName))
            .Select(fk => fk.SourceColumns.Zip(fk.ReferencedColumns).Select(p => (In(from, p.First.ColumnName), In(to, p.Second.ColumnName))).ToList())
            .ToList();

        keys.AddRange(
            from.Columns
                .Where(c => c.Column.IsForeignKey && Same(c.Column.ReferencedTableName, to.Table.TableName))
                .Where(c => !keys.Any(k => k.Count == 1 && k[0].Item1 == c))
                .Select(c => new List<(DmlColumn?, DmlColumn?)> { (c, In(to, c.Column.ReferencedColumnName ?? string.Empty)) })
        );

        return [.. keys.Where(k => k.All(p => p.Item1 is not null && p.Item2 is not null)).Select(k => k.Select(p => (p.Item1!, p.Item2!)).ToList())];
    }

    private static List<(DmlColumn Source, DmlColumn Referenced)> ForeignKey(DmlModel from, DmlModel to, Func<string> where)
    {
        var keys = ForeignKeys(from, to);
        return keys.Count switch
        {
            1 => keys[0],
            0 => throw new InvalidOperationException(
                $"{where()} names no keys, and {from.Type.Name} declares no foreign key to {to.Type.Name}: name the keys, "
                    + "or declare the foreign key."
            ),
            _ => throw new InvalidOperationException(
                $"{where()} names no keys, and {from.Type.Name} declares {keys.Count} foreign keys to {to.Type.Name}: "
                    + "name the keys, as [DmRelation(nameof(TheForeignKey))]."
            ),
        };
    }

    private static List<(DmlColumn Column, object? Value)> Values(DmlModel model, string[]? pairs, Func<string> where)
    {
        if (pairs is null)
        {
            return [];
        }

        if (pairs.Length % 2 != 0)
        {
            throw new InvalidOperationException($"{where()} gives fixed values as property and value pairs, and has an odd number of items.");
        }

        var values = new List<(DmlColumn, object?)>();
        for (var i = 0; i < pairs.Length; i += 2)
        {
            var column = Columns(model, [pairs[i]], where)[0];
            var type = Nullable.GetUnderlyingType(column.Property.PropertyType) ?? column.Property.PropertyType;
            object? value = pairs[i + 1] is null ? null
                : type == typeof(string) ? pairs[i + 1]
                : type.IsEnum ? Enum.Parse(type, pairs[i + 1], ignoreCase: true)
                : type == typeof(Guid) ? Guid.Parse(pairs[i + 1])
                : Convert.ChangeType(pairs[i + 1], type, CultureInfo.InvariantCulture);
            values.Add((column, value));
        }

        return values;
    }

    private static LambdaExpression ConditionOf(DmlModel owner, Type otherType, string name, Func<string> where)
    {
        const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        var type = owner.Type;
        var value = type.GetProperty(name, Static)?.GetValue(null)
            ?? type.GetField(name, Static)?.GetValue(null)
            ?? type.GetMethod(name, Static, Type.EmptyTypes)?.Invoke(null, null);

        bool Fits(LambdaExpression lambda) =>
            lambda.ReturnType == typeof(bool)
            && ((lambda.Parameters.Count == 1 && lambda.Parameters[0].Type == otherType)
                || (lambda.Parameters.Count == 2 && lambda.Parameters[0].Type == type && lambda.Parameters[1].Type == otherType));

        return value switch
        {
            LambdaExpression lambda when Fits(lambda) => lambda,
            _ => throw new InvalidOperationException(
                $"{where()} names the condition {name}, which must be a static property, field or method of {type.Name} "
                    + $"returning an Expression<Func<{otherType.Name}, bool>> or Expression<Func<{type.Name}, {otherType.Name}, bool>>."
            ),
        };
    }
}
