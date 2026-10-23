// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.DataAnnotations;

/// <summary>
/// Marks a property as a relation to another model's rows, never a column: one related row
/// (<c>public Customer? Customer</c>) or many (<c>public List&lt;OrderLine&gt;? Lines</c>). Relations are filled only
/// when a query asks (<c>Include</c>), and can be filtered on (<c>o =&gt; o.Lines!.Any(l =&gt; ...)</c>).
/// </summary>
/// <remarks>
/// <para>
/// With no keys named, the keys come from the one foreign key the two models declare between them, and naming no
/// keys where there is none, or several, is an error. A relation needs no foreign key in the database: name the keys,
/// and it joins on them.
/// </para>
/// <para>
/// A single key names the foreign key property: this model's for one related row, the other model's for many; the
/// other side is the referenced model's primary key.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [DmRelation]                                   // keys from the foreign key
/// public Customer? Customer { get; set; }
///
/// [DmRelation(nameof(ShipToId))]                 // two foreign keys to addresses: named
/// public Address? ShipTo { get; set; }
///
/// [DmRelation(                                   // EAV rows: no foreign key, a type value, a condition
///     thisKeys: [nameof(Id)],
///     otherKeys: [nameof(EavValue.ObjectId)],
///     otherValues: [nameof(EavValue.ObjectType), "order"],
///     condition: nameof(NotDeleted))]
/// public List&lt;EavValue&gt;? Attributes { get; set; }
///
/// static Expression&lt;Func&lt;EavValue, bool&gt;&gt; NotDeleted =&gt; v =&gt; !v.IsDeleted;
///
/// [DmRelation(through: typeof(ProductTag))]      // many-to-many, keys from the link model's two foreign keys
/// public List&lt;Tag&gt;? Tags { get; set; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DmRelationAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmRelationAttribute"/> class, with the keys named or taken from
    /// the models' foreign keys.
    /// </summary>
    /// <param name="thisKeys">This model's key properties, in pair order with <paramref name="otherKeys"/>.</param>
    /// <param name="otherKeys">The related model's key properties.</param>
    /// <param name="otherValues">
    /// Fixed values the related rows have, as property and value pairs: <c>[nameof(EavValue.ObjectType), "order"]</c>.
    /// </param>
    /// <param name="thisValues">Fixed values this model's rows must have for the relation to apply, as pairs.</param>
    /// <param name="condition">
    /// The name of a static property, field or method of this model returning an expression the related rows must
    /// match: an <c>Expression&lt;Func&lt;TOther, bool&gt;&gt;</c>, or <c>Expression&lt;Func&lt;TThis, TOther, bool&gt;&gt;</c>
    /// to compare with this row.
    /// </param>
    /// <param name="through">A link model for a many-to-many relation, whose two foreign keys join the models.</param>
    public DmRelationAttribute(
        string[]? thisKeys = null,
        string[]? otherKeys = null,
        string[]? otherValues = null,
        string[]? thisValues = null,
        string? condition = null,
        Type? through = null
    )
    {
        ThisKeys = thisKeys;
        OtherKeys = otherKeys;
        OtherValues = otherValues;
        ThisValues = thisValues;
        Condition = condition;
        Through = through;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmRelationAttribute"/> class, naming the foreign key property:
    /// this model's for one related row, the related model's for many.
    /// </summary>
    /// <param name="foreignKey">The foreign key property.</param>
    public DmRelationAttribute(string foreignKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(foreignKey);
        ForeignKey = foreignKey;
    }

    /// <summary>Gets this model's key properties, or null.</summary>
    public string[]? ThisKeys { get; }

    /// <summary>Gets the related model's key properties, or null.</summary>
    public string[]? OtherKeys { get; }

    /// <summary>Gets the related rows' fixed values, as property and value pairs, or null.</summary>
    public string[]? OtherValues { get; }

    /// <summary>Gets this model's fixed values, as property and value pairs, or null.</summary>
    public string[]? ThisValues { get; }

    /// <summary>Gets the name of the condition member, or null.</summary>
    public string? Condition { get; }

    /// <summary>Gets the link model of a many-to-many relation, or null.</summary>
    public Type? Through { get; }

    /// <summary>Gets the foreign key property named on its own, or null.</summary>
    public string? ForeignKey { get; }
}
