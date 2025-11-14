// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.DataAnnotations;

/// <summary>
/// Attribute to define a database column.
/// </summary>
/// <example>
/// [DmColumn("MyColumn", providerDataType: "nvarchar", length: 50, isNullable: true)]
/// public string? MyProperty { get; set; }
/// </example>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class DmColumnAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmColumnAttribute"/> class representing a column in a database table.
    /// </summary>
    /// <param name="columnName">The name of the column.</param>
    /// <param name="providerDataType">The data type of the column as defined by the database provider. Can be null if not specified.</param>
    /// <param name="length">The maximum length (in characters) for string or binary data types. Can be null if not specified.</param>
    /// <param name="precision">The total number of digits for numeric data types. Can be null if not specified.</param>
    /// <param name="scale">The number of digits to the right of the decimal point for numeric data types. Can be null if not specified.</param>
    /// <param name="checkExpression">A check constraint expression for the column.</param>
    /// <param name="defaultExpression">A default value expression for the column.</param>
    /// <param name="isNullable">Whether the column allows null values.</param>
    /// <param name="isPrimaryKey">Whether the column is part of the primary key.</param>
    /// <param name="isAutoIncrement">Whether the column is auto-incremented.</param>
    /// <param name="isUnique">Whether the column is unique.</param>
    /// <param name="isUnicode">Whether the column is Unicode.</param>
    /// <param name="isIndexed">Whether the column is indexed.</param>
    /// <param name="isForeignKey">Whether the column is a foreign key.</param>
    /// <param name="referencedTableName">The referenced table name if this column is a foreign key.</param>
    /// <param name="referencedColumnName">The referenced column name if this column is a foreign key.</param>
    /// <param name="onDelete">The action to take when a referenced row is deleted.</param>
    /// <param name="onUpdate">The action to take when a referenced row is updated.</param>
    public DmColumnAttribute(
        string? columnName = null,
        string? providerDataType = null,
        int length = 0,
        int precision = -1,
        int scale = -1,
        string? checkExpression = null,
        string? defaultExpression = null,
        bool isNullable = false,
        bool isPrimaryKey = false,
        bool isAutoIncrement = false,
        bool isUnique = false,
        bool isUnicode = false,
        bool isIndexed = false,
        bool isForeignKey = false,
        string? referencedTableName = null,
        string? referencedColumnName = null,
        DmForeignKeyAction onDelete = DmForeignKeyAction.NoAction,
        DmForeignKeyAction onUpdate = DmForeignKeyAction.NoAction
    )
    {
        ColumnName = columnName;
        ProviderDataType = providerDataType;
        Length = length == 0 ? null : length;
        Precision = precision == -1 ? null : precision;
        Scale = scale == -1 ? null : scale;
        CheckExpression = checkExpression;
        DefaultExpression = defaultExpression;
        IsNullable = isNullable;
        IsPrimaryKey = isPrimaryKey;
        IsAutoIncrement = isAutoIncrement;
        IsUnique = isUnique;
        IsUnicode = isUnicode;
        IsIndexed = isIndexed;
        IsForeignKey = isForeignKey;
        ReferencedTableName = referencedTableName;
        ReferencedColumnName = referencedColumnName;
        OnDelete = onDelete;
        OnUpdate = onUpdate;
    }

    /// <summary>
    /// Gets the column name.
    /// </summary>
    public string? ColumnName { get; }

    /// <summary>
    /// Gets the provider data type.
    /// Format of provider data types: {mysql:varchar(255),sqlserver:nvarchar(255)}.
    /// </summary>
    public string? ProviderDataType { get; }

    /// <summary>
    /// Gets the length of the column.
    /// </summary>
    public int? Length { get; }

    /// <summary>
    /// Gets the precision of the column.
    /// </summary>
    public int? Precision { get; }

    /// <summary>
    /// Gets the scale of the column.
    /// </summary>
    public int? Scale { get; }

    /// <summary>
    /// Gets the check expression for the column.
    /// </summary>
    public string? CheckExpression { get; }

    /// <summary>
    /// Gets the default expression for the column.
    /// </summary>
    public string? DefaultExpression { get; }

    /// <summary>
    /// Gets a value indicating whether the column is nullable.
    /// </summary>
    public bool IsNullable { get; }

    /// <summary>
    /// Gets a value indicating whether the column is a primary key.
    /// </summary>
    public bool IsPrimaryKey { get; }

    /// <summary>
    /// Gets a value indicating whether the column is auto-incremented.
    /// </summary>
    public bool IsAutoIncrement { get; }

    /// <summary>
    /// Gets a value indicating whether the column is unique.
    /// </summary>
    public bool IsUnique { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the column explicitly supports unicode characters.
    /// </summary>
    public bool IsUnicode { get; set; }

    /// <summary>
    /// Gets a value indicating whether the column is indexed.
    /// </summary>
    public bool IsIndexed { get; }

    /// <summary>
    /// Gets a value indicating whether the column is a foreign key.
    /// </summary>
    public bool IsForeignKey { get; }

    /// <summary>
    /// Gets the referenced table name for the foreign key.
    /// </summary>
    public string? ReferencedTableName { get; }

    /// <summary>
    /// Gets the referenced column name for the foreign key.
    /// </summary>
    public string? ReferencedColumnName { get; }

    /// <summary>
    /// Gets the action to perform on delete.
    /// </summary>
    public DmForeignKeyAction OnDelete { get; }

    /// <summary>
    /// Gets the action to perform on update.
    /// </summary>
    public DmForeignKeyAction OnUpdate { get; }
}
