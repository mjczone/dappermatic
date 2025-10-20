// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.DataAnnotations;

/// <summary>
/// Attribute to define foreign key constraints on a class or property.
/// </summary>
/// <example>
/// [DmForeignKeyConstraint(new[] { "ForeignKeyId" }, typeof(ReferencedEntity), new[] { "Id" }, "FK_MyTable_Ref", DmForeignKeyAction.Cascade, DmForeignKeyAction.NoAction)]
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class DmForeignKeyConstraintAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmForeignKeyConstraintAttribute"/> class.
    /// </summary>
    /// <param name="sourceColumnNames">The names of the source columns in the foreign key constraint. Only necessary when defined on a type.</param>
    /// <param name="referencedType">The type of the referenced entity in the foreign key constraint.</param>
    /// <param name="referencedTableName">The name of the referenced table. Use this or the referenced type, one or the other is required.</param>
    /// <param name="referencedColumnNames">The names of the referenced columns in the foreign key constraint.</param>
    /// <param name="constraintName">The name of the foreign key constraint.</param>
    /// <param name="onDelete">The action to take when a referenced row is deleted.</param>
    /// <param name="onUpdate">The action to take when a referenced row is updated.</param>
    public DmForeignKeyConstraintAttribute(
        string[]? sourceColumnNames = null,
        Type? referencedType = null,
        string? referencedTableName = null,
        string[]? referencedColumnNames = null,
        string? constraintName = null,
        DmForeignKeyAction onDelete = DmForeignKeyAction.NoAction,
        DmForeignKeyAction onUpdate = DmForeignKeyAction.NoAction
    )
    {
        // Either the referenced type or the referenced table name must be provided.
        if (referencedType == null && string.IsNullOrWhiteSpace(referencedTableName))
        {
            throw new ArgumentException(
                "Either a referenced type or a referenced table name must be provided",
                nameof(referencedType)
            );
        }

        SourceColumnNames = sourceColumnNames;
        ReferencedType = referencedType;
        ReferencedTableName = referencedTableName;
        ReferencedColumnNames = referencedColumnNames;
        ConstraintName = constraintName;
        OnDelete = onDelete;
        OnUpdate = onUpdate;
    }

    /// <summary>
    /// Gets the name of the foreign key constraint.
    /// </summary>
    public string? ConstraintName { get; }

    /// <summary>
    /// Gets the names of the source columns in the foreign key constraint.
    /// </summary>
    /// <remarks>
    /// These are the columns in the current entity that reference the foreign key.
    /// These are only necessary when the foreign key is defined on a type. When the
    /// foreign key is defined on a property, the source column names are derived from the property name.
    /// </remarks>
    public string[]? SourceColumnNames { get; }

    /// <summary>
    /// Gets the type of the referenced entity in the foreign key constraint.
    /// </summary>
    public Type? ReferencedType { get; }

    /// <summary>
    /// Gets the name of the reference table in the foreign key constraint.
    /// </summary>
    public string? ReferencedTableName { get; }

    /// <summary>
    /// Gets the names of the referenced columns in the foreign key constraint.
    /// </summary>
    public string[]? ReferencedColumnNames { get; }

    /// <summary>
    /// Gets the action to take when a referenced row is deleted.
    /// </summary>
    public DmForeignKeyAction OnDelete { get; }

    /// <summary>
    /// Gets the action to take when a referenced row is updated.
    /// </summary>
    public DmForeignKeyAction OnUpdate { get; }
}
