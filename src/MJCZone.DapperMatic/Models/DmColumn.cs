// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Represents a database column with various properties and methods to determine its characteristics.
/// </summary>
[Serializable]
public class DmColumn
{
    private string? _defaultExpression;
    private Func<DbProviderType, string>? _defaultExpressionFunc;
    private string? _checkExpression;
    private Func<DbProviderType, string>? _checkExpressionFunc;

    /// <summary>
    /// Initializes a new instance of the <see cref="DmColumn"/> class.
    /// </summary>
    public DmColumn() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmColumn"/> class. Used for quick creation when schema and table names are not needed, as when creating a table.
    /// </summary>
    /// <param name="columnName">The column name.</param>
    /// <param name="dotnetType">The .NET type of the column.</param>
    /// <param name="providerDataTypes">The provider data types.</param>
    /// <param name="length">The length of the column.</param>
    /// <param name="precision">The precision of the column.</param>
    /// <param name="scale">The scale of the column.</param>
    /// <param name="isNullable">Indicates whether the column is nullable.</param>
    /// <param name="isPrimaryKey">Indicates whether the column is a primary key.</param>
    /// <param name="isAutoIncrement">Indicates whether the column is auto-incremented.</param>
    /// <param name="isUnique">Indicates whether the column is unique.</param>
    /// <param name="isUnicode">Indicates whether the column explicitly supports unicode characters.</param>
    /// <param name="isIndexed">Indicates whether the column is indexed.</param>
    /// <param name="isForeignKey">Indicates whether the column is a foreign key.</param>
    /// <param name="referencedTableName">The referenced table name.</param>
    /// <param name="referencedColumnName">The referenced column name.</param>
    /// <param name="onDelete">The action on delete.</param>
    /// <param name="onUpdate">The action on update.</param>
    /// <param name="defaultExpression">The default expression.</param>
    /// <param name="defaultExpressionFunc">The default expression function for provider-specific defaults.</param>
    /// <param name="checkExpression">The check expression.</param>
    /// <param name="checkExpressionFunc">The check expression function for provider-specific checks.</param>
    [SetsRequiredMembers]
    public DmColumn(
        string columnName,
        Type? dotnetType = null,
        Dictionary<DbProviderType, string>? providerDataTypes = null,
        int? length = null,
        int? precision = null,
        int? scale = null,
        bool isNullable = false,
        bool isPrimaryKey = false,
        bool isAutoIncrement = false,
        bool isUnique = false,
        bool isUnicode = false,
        bool isIndexed = false,
        bool isForeignKey = false,
        string? referencedTableName = null,
        string? referencedColumnName = null,
        DmForeignKeyAction? onDelete = null,
        DmForeignKeyAction? onUpdate = null,
        string? defaultExpression = null,
        Func<DbProviderType, string>? defaultExpressionFunc = null,
        string? checkExpression = null,
        Func<DbProviderType, string>? checkExpressionFunc = null
    )
        : this(
            null,
            string.Empty,
            columnName,
            dotnetType,
            providerDataTypes,
            length,
            precision,
            scale,
            isNullable,
            isPrimaryKey,
            isAutoIncrement,
            isUnique,
            isUnicode,
            isIndexed,
            isForeignKey,
            referencedTableName,
            referencedColumnName,
            onDelete,
            onUpdate,
            checkExpression,
            checkExpressionFunc,
            defaultExpression,
            defaultExpressionFunc
        ) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmColumn"/> class with the specified parameters.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="dotnetType">The .NET type of the column.</param>
    /// <param name="providerDataTypes">The provider data types.</param>
    /// <param name="length">The length of the column.</param>
    /// <param name="precision">The precision of the column.</param>
    /// <param name="scale">The scale of the column.</param>
    /// <param name="isNullable">Indicates whether the column is nullable.</param>
    /// <param name="isPrimaryKey">Indicates whether the column is a primary key.</param>
    /// <param name="isAutoIncrement">Indicates whether the column is auto-incremented.</param>
    /// <param name="isUnique">Indicates whether the column is unique.</param>
    /// <param name="isUnicode">Indicates whether the column explicitly supports unicode characters.</param>
    /// <param name="isIndexed">Indicates whether the column is indexed.</param>
    /// <param name="isForeignKey">Indicates whether the column is a foreign key.</param>
    /// <param name="referencedTableName">The referenced table name.</param>
    /// <param name="referencedColumnName">The referenced column name.</param>
    /// <param name="onDelete">The action on delete.</param>
    /// <param name="onUpdate">The action on update.</param>
    /// <param name="defaultExpression">The default expression.</param>
    /// <param name="defaultExpressionFunc">The default expression function for provider-specific defaults.</param>
    /// <param name="checkExpression">The check expression.</param>
    /// <param name="checkExpressionFunc">The check expression function for provider-specific checks.</param>
    [SetsRequiredMembers]
    public DmColumn(
        string? schemaName,
        string tableName,
        string columnName,
        Type? dotnetType = null,
        Dictionary<DbProviderType, string>? providerDataTypes = null,
        int? length = null,
        int? precision = null,
        int? scale = null,
        bool isNullable = false,
        bool isPrimaryKey = false,
        bool isAutoIncrement = false,
        bool isUnique = false,
        bool isUnicode = false,
        bool isIndexed = false,
        bool isForeignKey = false,
        string? referencedTableName = null,
        string? referencedColumnName = null,
        DmForeignKeyAction? onDelete = null,
        DmForeignKeyAction? onUpdate = null,
        string? defaultExpression = null,
        Func<DbProviderType, string>? defaultExpressionFunc = null,
        string? checkExpression = null,
        Func<DbProviderType, string>? checkExpressionFunc = null
    )
    {
        SchemaName = schemaName;
        TableName = tableName;
        ColumnName = columnName;
        DotnetType = dotnetType;
        ProviderDataTypes = providerDataTypes ?? [];
        Length = length;
        Precision = precision;
        Scale = scale;
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
        CheckExpression = checkExpression;
        CheckExpressionFunc = checkExpressionFunc;
        DefaultExpression = defaultExpression;
        DefaultExpressionFunc = defaultExpressionFunc;
    }

    /// <summary>
    /// Gets or sets the schema name.
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Gets or sets the table name.
    /// </summary>
    public required string TableName { get; set; }

    /// <summary>
    /// Gets or sets the column name.
    /// </summary>
    public required string ColumnName { get; set; }

    /// <summary>
    /// Gets or sets the .NET type of the column.
    /// </summary>
    public Type? DotnetType { get; set; }

    /// <summary>
    /// Gets the provider data types. The FULL native provider data type. This is the data type that the provider uses to
    /// store the data (e.g. "INTEGER", "DECIMAL(14,3)", "VARCHAR(255)", "TEXT", "BLOB", etc.)
    /// </summary>
    /// <remarks>
    /// The provider data type should include the length, precision, and scale if applicable.
    /// </remarks>
    public Dictionary<DbProviderType, string> ProviderDataTypes { get; } = new();

    /// <summary>
    /// Gets or sets the length of the column.
    /// </summary>
    public int? Length { get; set; }

    /// <summary>
    /// Gets or sets the precision of the column.
    /// </summary>
    public int? Precision { get; set; }

    /// <summary>
    /// Gets or sets the scale of the column.
    /// </summary>
    public int? Scale { get; set; }

    /// <summary>
    /// Gets or sets the check expression.
    /// Setting this property will clear the CheckExpressionFunc property.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Microsoft.Design",
        "CA1721:PropertyNamesShouldNotMatchGetMethods",
        Justification = "CheckExpression property and GetCheckExpression method serve different purposes - property for static values, method for provider-specific evaluation"
    )]
    public string? CheckExpression
    {
        get => _checkExpression;
        set
        {
            _checkExpression = value;
            if (_checkExpression == null)
            {
                // It could be user was just trying to make sure this was null, so
                // don't clear the func in that case.
                return;
            }

            _checkExpressionFunc = null;
        }
    }

    /// <summary>
    /// Gets or sets the check expression function that generates provider-specific check expressions.
    /// Setting this property will clear the CheckExpression property.
    /// </summary>
    public Func<DbProviderType, string>? CheckExpressionFunc
    {
        get => _checkExpressionFunc;
        set
        {
            _checkExpressionFunc = value;
            if (_checkExpressionFunc == null)
            {
                // It could be user was just trying to make sure this was null, so
                // don't clear the check expression in that case.
                return;
            }

            _checkExpression = null;
        }
    }

    /// <summary>
    /// Gets or sets the default expression.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Microsoft.Design",
        "CA1721:PropertyNamesShouldNotMatchGetMethods",
        Justification = "DefaultExpression property and GetDefaultExpression method serve different purposes - property for static values, method for provider-specific evaluation"
    )]
    public string? DefaultExpression
    {
        get => _defaultExpression;
        set
        {
            _defaultExpression = value;
            if (_defaultExpression == null)
            {
                // It could be user was just trying to make sure this was null, so
                // don't clear the func in that case.
                return;
            }

            _defaultExpressionFunc = null;
        }
    }

    /// <summary>
    /// Gets or sets the default expression function that generates provider-specific default values.
    /// Setting this property will clear the DefaultExpression property.
    /// </summary>
    public Func<DbProviderType, string>? DefaultExpressionFunc
    {
        get => _defaultExpressionFunc;
        set
        {
            _defaultExpressionFunc = value;
            if (_defaultExpressionFunc == null)
            {
                // It could be user was just trying to make sure this was null, so
                // don't clear the default expression in that case.
                return;
            }

            _defaultExpression = null;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the column is nullable.
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is a primary key.
    /// </summary>
    public bool IsPrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is auto-incremented.
    /// </summary>
    public bool IsAutoIncrement { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is unique.
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column explicitly supports unicode characters.
    /// </summary>
    public bool IsUnicode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is of a fixed length.
    /// </summary>
    public bool IsFixedLength { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is indexed.
    /// </summary>
    public bool IsIndexed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is a foreign key.
    /// </summary>
    public bool IsForeignKey { get; set; }

    /// <summary>
    /// Gets or sets the referenced table name.
    /// </summary>
    public string? ReferencedTableName { get; set; }

    /// <summary>
    /// Gets or sets the referenced column name.
    /// </summary>
    public string? ReferencedColumnName { get; set; }

    /// <summary>
    /// Gets or sets the action on delete.
    /// </summary>
    public DmForeignKeyAction? OnDelete { get; set; }

    /// <summary>
    /// Gets or sets the action on update.
    /// </summary>
    public DmForeignKeyAction? OnUpdate { get; set; }

    /// <summary>
    /// Determines whether the column is numeric.
    /// </summary>
    /// <returns><c>true</c> if the column is numeric; otherwise, <c>false</c>.</returns>
    public bool IsNumeric()
    {
        return DotnetType == typeof(byte)
            || DotnetType == typeof(sbyte)
            || DotnetType == typeof(short)
            || DotnetType == typeof(ushort)
            || DotnetType == typeof(int)
            || DotnetType == typeof(uint)
            || DotnetType == typeof(long)
            || DotnetType == typeof(ulong)
            || DotnetType == typeof(float)
            || DotnetType == typeof(double)
            || DotnetType == typeof(decimal);
    }

    /// <summary>
    /// Determines whether the column is text.
    /// </summary>
    /// <returns><c>true</c> if the column is text; otherwise, <c>false</c>.</returns>
    public bool IsText()
    {
        return DotnetType == typeof(string) || DotnetType == typeof(char) || DotnetType == typeof(char[]);
    }

    /// <summary>
    /// Determines whether the column is a date/time type.
    /// </summary>
    /// <returns><c>true</c> if the column is a date/time type; otherwise, <c>false</c>.</returns>
    public bool IsDateTime()
    {
        return DotnetType == typeof(DateTime) || DotnetType == typeof(DateTimeOffset);
    }

    /// <summary>
    /// Determines whether the column is a boolean type.
    /// </summary>
    /// <returns><c>true</c> if the column is a boolean type; otherwise, <c>false</c>.</returns>
    public bool IsBoolean()
    {
        return DotnetType == typeof(bool);
    }

    /// <summary>
    /// Determines whether the column is a binary type.
    /// </summary>
    /// <returns><c>true</c> if the column is a binary type; otherwise, <c>false</c>.</returns>
    public bool IsBinary()
    {
        return DotnetType == typeof(byte[]);
    }

    /// <summary>
    /// Determines whether the column is a GUID type.
    /// </summary>
    /// <returns><c>true</c> if the column is a GUID type; otherwise, <c>false</c>.</returns>
    public bool IsGuid()
    {
        return DotnetType == typeof(Guid);
    }

    /// <summary>
    /// Determines whether the column is an enum type.
    /// </summary>
    /// <returns><c>true</c> if the column is an enum type; otherwise, <c>false</c>.</returns>
    public bool IsEnum()
    {
        return DotnetType?.IsEnum == true;
    }

    /// <summary>
    /// Determines whether the column is an array type.
    /// </summary>
    /// <returns><c>true</c> if the column is an array type; otherwise, <c>false</c>.</returns>
    public bool IsArray()
    {
        return DotnetType?.IsArray == true;
    }

    /// <summary>
    /// Determines whether the column is a dictionary type.
    /// </summary>
    /// <returns><c>true</c> if the column is a dictionary type; otherwise, <c>false</c>.</returns>
    public bool IsDictionary()
    {
        return DotnetType?.IsGenericType == true && DotnetType.GetGenericTypeDefinition() == typeof(Dictionary<,>);
    }

    /// <summary>
    /// Determines whether the column is an enumerable type.
    /// </summary>
    /// <returns><c>true</c> if the column is an enumerable type; otherwise, <c>false</c>.</returns>
    public bool IsEnumerable()
    {
        return typeof(IEnumerable<>).IsAssignableFrom(DotnetType);
    }

    /// <summary>
    /// Gets the type category of the column.
    /// </summary>
    /// <returns>The type category of the column.</returns>
    public string GetTypeCategory()
    {
        if (IsNumeric())
        {
            return "Numeric";
        }

        if (IsText())
        {
            return "Text";
        }

        if (IsDateTime())
        {
            return "DateTime";
        }

        if (IsBoolean())
        {
            return "Boolean";
        }

        if (IsBinary())
        {
            return "Binary";
        }

        if (IsGuid())
        {
            return "Guid";
        }

        if (IsEnum())
        {
            return "Enum";
        }

        if (IsArray())
        {
            return "Array";
        }

        if (IsDictionary())
        {
            return "Dictionary";
        }

        return IsEnumerable() ? "Enumerable" : "Unknown";
    }

    /// <summary>
    /// Returns a string representation of the column definition.
    /// </summary>
    /// <returns>A string representation of the column definition.</returns>
    public override string ToString()
    {
        return $"{ColumnName} ({string.Join(", ", ProviderDataTypes.Select(pdt => $"{pdt.Key}={pdt.Value}"))}) {(IsNullable ? "NULL" : "NOT NULL")}"
            + $"{(IsPrimaryKey ? " PRIMARY KEY" : string.Empty)}"
            + $"{(IsUnique ? " UNIQUE" : string.Empty)}"
            + $"{(IsIndexed ? " INDEXED" : string.Empty)}"
            + $"{(IsForeignKey ? $" FOREIGN KEY({ReferencedTableName ?? string.Empty}) REFERENCES({ReferencedColumnName ?? string.Empty})" : string.Empty)}"
            + $"{(IsAutoIncrement ? " AUTOINCREMENT" : string.Empty)}"
            + $"{(!string.IsNullOrWhiteSpace(CheckExpression) ? $" CHECK ({CheckExpression})" : string.Empty)}"
            + $"{(!string.IsNullOrWhiteSpace(DefaultExpression) ? $" DEFAULT {(DefaultExpression.Contains(' ', StringComparison.OrdinalIgnoreCase) ? $"({DefaultExpression})" : DefaultExpression)}" : string.Empty)}";
    }

    /// <summary>
    /// Gets the provider data type for the specified provider.
    /// </summary>
    /// <param name="providerType">The provider type.</param>
    /// <returns>The provider data type for the specified provider.</returns>
    public string? GetProviderDataType(DbProviderType providerType)
    {
        return ProviderDataTypes.TryGetValue(providerType, out var providerDataType) ? providerDataType : null;
    }

    /// <summary>
    /// Sets the provider data type for the specified provider.
    /// </summary>
    /// <param name="providerType">The provider type.</param>
    /// <param name="providerDataType">The provider data type.</param>
    /// <returns>The current <see cref="DmColumn"/> instance.</returns>
    public DmColumn SetProviderDataType(DbProviderType providerType, string providerDataType)
    {
        ProviderDataTypes[providerType] = providerDataType;
        return this;
    }

    /// <summary>
    /// Gets the effective default expression for the specified provider type.
    /// Returns the result of DefaultExpressionFunc if set, otherwise returns DefaultExpression.
    /// </summary>
    /// <param name="providerType">The database provider type.</param>
    /// <returns>The default expression for the specified provider, or null if neither is set.</returns>
    public string? GetDefaultExpression(DbProviderType providerType)
    {
        return _defaultExpressionFunc != null ? _defaultExpressionFunc(providerType) : _defaultExpression;
    }

    /// <summary>
    /// Gets the effective check expression for the specified provider type.
    /// Returns the result of CheckExpressionFunc if set, otherwise returns CheckExpression.
    /// </summary>
    /// <param name="providerType">The database provider type.</param>
    /// <returns>The check expression for the specified provider, or null if neither is set.</returns>
    public string? GetCheckExpression(DbProviderType providerType)
    {
        return _checkExpressionFunc != null ? _checkExpressionFunc(providerType) : _checkExpression;
    }
}
