// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Providers;

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Factory for creating DmTable instances for types.
/// </summary>
public static class DmTableFactory
{
    private static readonly ConcurrentDictionary<Type, DmTable> _cache = new();
    private static readonly ConcurrentDictionary<Type, Dictionary<string, DmColumn>> _propertyCache = new();

    private static Action<Type, DmTable>? _customMappingAction;

    /// <summary>
    /// Configure ahead of time any custom configuration for mapping types to DmTable instances. Call this
    /// before the application attempts to map types to DmTable instances, as the mappings are cached once generated
    /// the very first time.
    /// </summary>
    /// <param name="configure">A delegate that receives the Type that is currently being mapped to a DmTable, and an initial DmTable that represents the default mapping before any customizations are applied. The delegate will run when the GetTable method is run for the first time each particular type.</param>
    public static void Configure(Action<Type, DmTable> configure)
    {
        _customMappingAction = configure;
    }

    /// <summary>
    /// Configure a specific type to your liking. This method can be used to customize the behavior of DmTable generation.
    /// </summary>
    /// <param name="configure">A delegate that receives an initial DmTable that represents the default mapping before any customizations are applied. The type mapping is created immediately and the delegate is run immediately as well.</param>
    /// <typeparam name="T">Type that should be mapped to a DmTable instance.</typeparam>
    public static void Configure<T>(Action<DmTable> configure)
    {
        Configure(typeof(T), configure);
    }

    /// <summary>
    /// Configure a specific type to your liking. This method can be used to customize the behavior of DmTable generation.
    /// </summary>
    /// <param name="type">Type that should be mapped to a DmTable instance.</param>
    /// <param name="configure">A delegate that receives an initial DmTable that represents the default mapping before any customizations are applied. The type mapping is created immediately and the delegate is run immediately as well.</param>
    public static void Configure(Type type, Action<DmTable> configure)
    {
        var table = GetTable(type);
        configure(table);
        _cache.AddOrUpdate(type, table, (_, _) => table);
    }

    /// <summary>
    /// Returns an instance of a DmTable for the given type. If the type is not a valid DmTable,
    /// denoted by the use of a DmTableAAttribute on the class, this method returns null.
    /// </summary>
    /// <param name="type">The type to map to a DmTable instance.</param>
    /// <returns>A DmTable instance representing the type.</returns>
    public static DmTable GetTable(Type type)
    {
        ArgumentNullException.ThrowIfNull(type, nameof(type));

        if (_cache.TryGetValue(type, out var table))
        {
            return table;
        }

        var propertyNameToColumnMap = new Dictionary<string, DmColumn>();
        table = GetTableInternal(type, propertyNameToColumnMap);

        _customMappingAction?.Invoke(type, table);

        _cache.TryAdd(type, table);
        _propertyCache.TryAdd(type, propertyNameToColumnMap);
        return table;
    }

    /// <summary>
    /// There are times we just need the schema name and table name for a type, without the full DmTable instance.
    /// We also may need this information without causing a recursive call to GetTable, which may have foreign keys
    /// that reference a table with a foreign key back to it, leading to a stack overflow.
    /// </summary>
    /// <param name="type">The type to extract a schema and table name for.</param>
    /// <param name="ignoreCache">
    /// If true, the cache will be ignored and the schema and table name will be extracted from the type's attributes.
    /// If false, the cache will be checked first, and if the type is found in the cache, the cached values will be returned.
    /// </param>
    /// <returns>A tuple containing the schema name and table name for the type.</returns>
    public static (string? schemaName, string tableName) GetTableName(Type type, bool ignoreCache = false)
    {
        ArgumentNullException.ThrowIfNull(type, nameof(type));

        if (!ignoreCache && _cache.TryGetValue(type, out var table))
        {
            return (table.SchemaName ?? string.Empty, table.TableName ?? type.Name);
        }

        var classAttributes = type.GetCustomAttributes().ToArray();

        var tableAttribute = type.GetCustomAttribute<DmTableAttribute>() ?? new DmTableAttribute(null, type.Name);

        var schemaName =
            classAttributes
                .Select(ca =>
                {
                    var paType = ca.GetType();
                    if (ca is DmTableAttribute dca && !string.IsNullOrWhiteSpace(dca.SchemaName))
                    {
                        return dca.SchemaName;
                    }
                    // EF Core
                    if (
                        ca is System.ComponentModel.DataAnnotations.Schema.TableAttribute ta
                        && !string.IsNullOrWhiteSpace(ta.Schema)
                    )
                    {
                        return ta.Schema;
                    }
                    // ServiceStack.OrmLite
                    if (paType.Name == "SchemaAttribute" && ca.TryGetPropertyValue<string>("Name", out var name))
                    {
                        return name;
                    }

                    return null;
                })
                .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
            ?? tableAttribute?.SchemaName
            ?? null;

        var tableName =
            classAttributes
                .Select(ca =>
                {
                    var paType = ca.GetType();
                    if (ca is DmTableAttribute dca && !string.IsNullOrWhiteSpace(dca.TableName))
                    {
                        return dca.TableName;
                    }
                    // EF Core
                    if (
                        ca is System.ComponentModel.DataAnnotations.Schema.TableAttribute ta
                        && !string.IsNullOrWhiteSpace(ta.Name)
                    )
                    {
                        return ta.Name;
                    }
                    // ServiceStack.OrmLite
                    if (
                        paType.Name == "AliasAttribute"
                        && ca.TryGetPropertyValue<string>("Name", out var name)
                        && !string.IsNullOrWhiteSpace(name)
                    )
                    {
                        return name;
                    }

                    return null;
                })
                .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
            ?? tableAttribute?.TableName;

        if (string.IsNullOrWhiteSpace(tableName))
        {
            // If no table name is specified, use the type name as the table name
            tableName = type.Name;
        }

        return (schemaName, tableName);
    }

    /// <summary>
    /// Internal method to clear the cache for testing purposes only.
    /// This is not part of the public API and should not be used in production code.
    /// </summary>
    internal static void ClearCacheForTesting()
    {
        _cache.Clear();
        _propertyCache.Clear();
        _customMappingAction = null;
    }

    /// <summary>
    /// Generates a DmTable instance for the given type, including all column mappings and constraints.
    /// </summary>
    /// <param name="type">The type to map to a DmTable instance.</param>
    /// <param name="propertyMappings">A dictionary to store property to column mappings.</param>
    /// <returns>A DmTable instance representing the type.</returns>
    private static DmTable GetTableInternal(Type type, Dictionary<string, DmColumn> propertyMappings)
    {
        var (schemaName, tableName) = GetTableName(type, ignoreCache: true);

        // columns must bind to public properties that can be both read and written
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);

        DmPrimaryKeyConstraint? primaryKey = null;
        var columns = new List<DmColumn>();
        var checkConstraints = new List<DmCheckConstraint>();
        var defaultConstraints = new List<DmDefaultConstraint>();
        var uniqueConstraints = new List<DmUniqueConstraint>();
        var foreignKeyConstraints = new List<DmForeignKeyConstraint>();
        var indexes = new List<DmIndex>();

        foreach (var property in properties)
        {
            var propertyAttributes = property.GetCustomAttributes().ToArray();

            var hasIgnoreAttribute = propertyAttributes.Any(pa =>
            {
                var paType = pa.GetType();
                return pa is DmIgnoreAttribute
                    // EF Core
                    || pa is System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute
                    // ServiceStack.OrmLite
                    || paType.Name == "IgnoreAttribute";
            });

            if (hasIgnoreAttribute)
            {
                continue;
            }

            var columnAttribute = property.GetCustomAttribute<DmColumnAttribute>();
            var columnName =
                propertyAttributes
                    .Select(pa =>
                    {
                        var paType = pa.GetType();
                        if (pa is DmColumnAttribute dca && !string.IsNullOrWhiteSpace(dca.ColumnName))
                        {
                            return dca.ColumnName;
                        }
                        // EF Core
                        else if (
                            pa is System.ComponentModel.DataAnnotations.Schema.ColumnAttribute ca
                            && !string.IsNullOrWhiteSpace(ca.Name)
                        )
                        {
                            return ca.Name;
                        }
                        // ServiceStack.OrmLite
                        else if (
                            paType.Name == "AliasAttribute"
                            && pa.TryGetPropertyValue<string>("Name", out var name)
                            && !string.IsNullOrWhiteSpace(name)
                        )
                        {
                            return name;
                        }

                        return null;
                    })
                    .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                ?? columnAttribute?.ColumnName;

            if (string.IsNullOrWhiteSpace(columnName))
            {
                // If no column name is specified, use the property name as the column name
                columnName = property.Name;
            }

            var isPrimaryKey =
                columnAttribute?.IsPrimaryKey == true
                || propertyAttributes.Any(pa =>
                {
                    var paType = pa.GetType();
                    return pa is DmPrimaryKeyConstraintAttribute
                        // EF Core
                        || pa is System.ComponentModel.DataAnnotations.KeyAttribute
                        // ServiceStack.OrmLite
                        || paType.Name == "PrimaryKeyAttribute";
                });

            var isRequired = propertyAttributes.Any(pa =>
            {
                var paType = pa.GetType();
                return
                    // EF Core
                    pa is System.ComponentModel.DataAnnotations.RequiredAttribute
                    // ServiceStack.OrmLite
                    || paType.Name == "RequiredAttribute";
            });

            // Format of provider data types: {mysql:varchar(255),sqlserver:nvarchar(255)}
            // Parse using helper that properly handles parameterized types like decimal(10,2)
            var providerDataTypes = TypeMappingHelpers.ParseProviderDataTypes(columnAttribute?.ProviderDataType);

            // If there is no DmColumnAttribute, the property is nullable by default, IF it's a reference type or a nullable type.
            var isNullable = columnAttribute == null ? property.PropertyType.IsNullable() : columnAttribute.IsNullable;

            // If the property is required, it cannot be nullable.
            if (isRequired && isNullable)
            {
                isNullable = false;
            }

            var column = new DmColumn(
                schemaName,
                tableName,
                columnName,
                property.PropertyType,
                providerDataTypes.Count != 0 ? providerDataTypes : null,
                columnAttribute?.Length,
                columnAttribute?.Precision,
                columnAttribute?.Scale,
                isNullable,
                columnAttribute?.IsPrimaryKey ?? false,
                columnAttribute?.IsAutoIncrement ?? false,
                columnAttribute?.IsUnique ?? false,
                columnAttribute?.IsUnicode ?? false,
                columnAttribute?.IsIndexed ?? false,
                columnAttribute?.IsForeignKey ?? false,
                string.IsNullOrWhiteSpace(columnAttribute?.ReferencedTableName)
                    ? null
                    : columnAttribute.ReferencedTableName,
                string.IsNullOrWhiteSpace(columnAttribute?.ReferencedColumnName)
                    ? null
                    : columnAttribute.ReferencedColumnName,
                columnAttribute?.OnDelete ?? null,
                columnAttribute?.OnUpdate ?? null,
                checkExpression: string.IsNullOrWhiteSpace(columnAttribute?.CheckExpression)
                    ? null
                    : columnAttribute.CheckExpression,
                defaultExpression: string.IsNullOrWhiteSpace(columnAttribute?.DefaultExpression)
                    ? null
                    : columnAttribute.DefaultExpression
            );
            columns.Add(column);
            propertyMappings.Add(property.Name, column);

            if (column.Length == null)
            {
                var stringLengthAttribute =
                    property.GetCustomAttribute<System.ComponentModel.DataAnnotations.StringLengthAttribute>();
                if (stringLengthAttribute != null)
                {
                    column.Length = stringLengthAttribute.MaximumLength;
                }
                else
                {
                    var maxLengthAttribute =
                        property.GetCustomAttribute<System.ComponentModel.DataAnnotations.MaxLengthAttribute>();
                    if (maxLengthAttribute != null)
                    {
                        column.Length = maxLengthAttribute.Length;
                    }
                }
            }

            // set primary key if present
            var columnPrimaryKeyAttribute = property.GetCustomAttribute<DmPrimaryKeyConstraintAttribute>();
            if (columnPrimaryKeyAttribute != null)
            {
                column.IsPrimaryKey = true;
                if (primaryKey == null)
                {
                    primaryKey = new DmPrimaryKeyConstraint(
                        schemaName,
                        tableName,
                        !string.IsNullOrWhiteSpace(columnPrimaryKeyAttribute.ConstraintName)
                            ? columnPrimaryKeyAttribute.ConstraintName
                            : string.Empty,
                        [new(columnName)]
                    );
                }
                else
                {
                    primaryKey.Columns = [.. new List<DmOrderedColumn>(primaryKey.Columns) { new(columnName) }];
                    if (!string.IsNullOrWhiteSpace(columnPrimaryKeyAttribute.ConstraintName))
                    {
                        primaryKey.ConstraintName = columnPrimaryKeyAttribute.ConstraintName;
                    }
                }
            }
            else if (isPrimaryKey)
            {
                column.IsPrimaryKey = true;
                if (primaryKey == null)
                {
                    primaryKey = new DmPrimaryKeyConstraint(schemaName, tableName, string.Empty, [new(columnName)]);
                }
                else
                {
                    primaryKey.Columns = [.. new List<DmOrderedColumn>(primaryKey.Columns) { new(columnName) }];
                }
            }

            // set check expression if present
            var columnCheckConstraintAttribute = property.GetCustomAttribute<DmCheckConstraintAttribute>();
            if (columnCheckConstraintAttribute != null)
            {
                var checkConstraint = new DmCheckConstraint(
                    schemaName,
                    tableName,
                    columnName,
                    !string.IsNullOrWhiteSpace(columnCheckConstraintAttribute.ConstraintName)
                        ? columnCheckConstraintAttribute.ConstraintName
                        : DbProviderUtils.GenerateCheckConstraintName(tableName, columnName),
                    columnCheckConstraintAttribute.Expression
                );
                checkConstraints.Add(checkConstraint);

                column.CheckExpression = columnCheckConstraintAttribute.Expression;
            }

            // set default expression if present
            var columnDefaultConstraintAttribute = property.GetCustomAttribute<DmDefaultConstraintAttribute>();
            if (columnDefaultConstraintAttribute != null)
            {
                var defaultConstraint = new DmDefaultConstraint(
                    schemaName,
                    tableName,
                    columnName,
                    !string.IsNullOrWhiteSpace(columnDefaultConstraintAttribute.ConstraintName)
                        ? columnDefaultConstraintAttribute.ConstraintName
                        : DbProviderUtils.GenerateDefaultConstraintName(tableName, columnName),
                    columnDefaultConstraintAttribute.Expression
                );
                defaultConstraints.Add(defaultConstraint);

                column.DefaultExpression = columnDefaultConstraintAttribute.Expression;
            }

            // set unique constraint if present
            var columnUniqueConstraintAttribute = property.GetCustomAttribute<DmUniqueConstraintAttribute>();
            if (columnUniqueConstraintAttribute != null)
            {
                var uniqueConstraint = new DmUniqueConstraint(
                    schemaName,
                    tableName,
                    !string.IsNullOrWhiteSpace(columnUniqueConstraintAttribute.ConstraintName)
                        ? columnUniqueConstraintAttribute.ConstraintName
                        : DbProviderUtils.GenerateUniqueConstraintName(tableName, columnName),
                    [new(columnName)]
                );
                uniqueConstraints.Add(uniqueConstraint);

                column.IsUnique = true;
            }

            // set index if present
            var columnIndexAttribute = property.GetCustomAttribute<DmIndexAttribute>();
            if (columnIndexAttribute != null)
            {
                var index = new DmIndex(
                    schemaName,
                    tableName,
                    !string.IsNullOrWhiteSpace(columnIndexAttribute.IndexName)
                        ? columnIndexAttribute.IndexName
                        : DbProviderUtils.GenerateIndexName(tableName, columnName),
                    [new(columnName)],
                    isUnique: columnIndexAttribute.IsUnique
                );
                indexes.Add(index);

                column.IsIndexed = true;
                if (index.IsUnique)
                {
                    column.IsUnique = true;
                }
            }
            else
            {
                var indexAttribute = propertyAttributes.FirstOrDefault(pa =>
                    pa.GetType().FullName == "Microsoft.EntityFrameworkCore.IndexAttribute"
                );
                if (indexAttribute != null)
                {
                    var isUnique = indexAttribute.TryGetPropertyValue<bool>("IsUnique", out var u) && u;
                    var indexName =
                        (indexAttribute.TryGetPropertyValue<string>("Name", out var n) && !string.IsNullOrWhiteSpace(n))
                            ? n
                            : DbProviderUtils.GenerateIndexName(tableName, columnName);
                    var index = new DmIndex(schemaName, tableName, indexName, [new(columnName)], isUnique);
                    indexes.Add(index);

                    column.IsIndexed = true;
                    if (index.IsUnique)
                    {
                        column.IsUnique = true;
                    }
                }
            }

            // set foreign key constraint if present
            var columnForeignKeyConstraintAttribute = property.GetCustomAttribute<DmForeignKeyConstraintAttribute>();
            if (columnForeignKeyConstraintAttribute != null)
            {
                var referencedTableName = columnForeignKeyConstraintAttribute.ReferencedTableName;
                if (
                    string.IsNullOrWhiteSpace(referencedTableName)
                    && columnForeignKeyConstraintAttribute.ReferencedType != null
                )
                {
                    // Populate the referenced table name from the referenced type
                    var referencedTypeInfo = GetTableName(
                        columnForeignKeyConstraintAttribute.ReferencedType,
                        ignoreCache: true
                    );
                    referencedTableName = referencedTypeInfo.tableName;
                }
                var referencedColumnNames = columnForeignKeyConstraintAttribute.ReferencedColumnNames;
                var onDelete = columnForeignKeyConstraintAttribute.OnDelete;
                var onUpdate = columnForeignKeyConstraintAttribute.OnUpdate;
                if (
                    !string.IsNullOrWhiteSpace(referencedTableName)
                    && referencedColumnNames != null
                    && referencedColumnNames.Length > 0
                    && !string.IsNullOrWhiteSpace(referencedColumnNames[0])
                )
                {
                    var constraintName = !string.IsNullOrWhiteSpace(columnForeignKeyConstraintAttribute.ConstraintName)
                        ? columnForeignKeyConstraintAttribute.ConstraintName
                        : DbProviderUtils.GenerateForeignKeyConstraintName(
                            tableName,
                            columnName,
                            referencedTableName,
                            referencedColumnNames[0]
                        );
                    var foreignKeyConstraint = new DmForeignKeyConstraint(
                        schemaName,
                        tableName,
                        constraintName,
                        [new(columnName)],
                        referencedTableName,
                        [new(referencedColumnNames[0])],
                        onDelete,
                        onUpdate
                    );
                    foreignKeyConstraints.Add(foreignKeyConstraint);

                    column.IsForeignKey = true;
                    column.ReferencedTableName = referencedTableName;
                    column.ReferencedColumnName = referencedColumnNames[0];
                    column.OnDelete = onDelete;
                    column.OnUpdate = onUpdate;
                }
            }
            else
            {
                var foreignKeyAttribute =
                    property.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ForeignKeyAttribute>();
                if (foreignKeyAttribute != null)
                {
                    var inversePropertyAttribute =
                        property.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.InversePropertyAttribute>();
                    var referencedTableName = foreignKeyAttribute.Name;
                    // TODO: figure out a way to derive the referenced column name
                    var referencedColumnNames = new[] { inversePropertyAttribute?.Property ?? "id" };
                    var onDelete = DmForeignKeyAction.NoAction;
                    var onUpdate = DmForeignKeyAction.NoAction;
                    var constraintName = DbProviderUtils.GenerateForeignKeyConstraintName(
                        tableName,
                        columnName,
                        referencedTableName,
                        referencedColumnNames[0]
                    );
                    var foreignKeyConstraint = new DmForeignKeyConstraint(
                        schemaName,
                        tableName,
                        constraintName,
                        [new(columnName)],
                        referencedTableName,
                        [new(referencedColumnNames[0])],
                        onDelete,
                        onUpdate
                    );
                    foreignKeyConstraints.Add(foreignKeyConstraint);

                    column.IsForeignKey = true;
                    column.ReferencedTableName = referencedTableName;
                    column.ReferencedColumnName = referencedColumnNames[0];
                    column.OnDelete = onDelete;
                    column.OnUpdate = onUpdate;
                }
            }
        }

        // TRUST that the developer knows what they are doing and not creating double the amount of attributes then
        // necessary. Class level attributes get used without questioning.

        var cpa = type.GetCustomAttribute<DmPrimaryKeyConstraintAttribute>();
        if (cpa != null && cpa.Columns != null && cpa.Columns.Length > 0)
        {
            var constraintName = !string.IsNullOrWhiteSpace(cpa.ConstraintName) ? cpa.ConstraintName : string.Empty;

            primaryKey = new DmPrimaryKeyConstraint(
                schemaName,
                tableName,
                constraintName,
                [.. cpa.Columns.Select(c => new DmOrderedColumn(c))]
            );

            // flag the column as part of the primary key
            foreach (var c in cpa.Columns!)
            {
                var column = columns.FirstOrDefault(col =>
                    col.ColumnName.Equals(c, StringComparison.OrdinalIgnoreCase)
                );
                if (column != null)
                {
                    column.IsPrimaryKey = true;
                }
            }
        }

        if (primaryKey != null && string.IsNullOrWhiteSpace(primaryKey.ConstraintName))
        {
            primaryKey.ConstraintName = DbProviderUtils.GeneratePrimaryKeyConstraintName(
                tableName,
                primaryKey.Columns.Select(c => c.ColumnName).ToArray()
            );
        }

        var ccas = type.GetCustomAttributes<DmCheckConstraintAttribute>();
        var ccaId = 1;
        foreach (var cca in ccas)
        {
            if (string.IsNullOrWhiteSpace(cca.Expression))
            {
                continue;
            }

            var constraintName = !string.IsNullOrWhiteSpace(cca.ConstraintName)
                ? cca.ConstraintName
                : DbProviderUtils.GenerateCheckConstraintName(tableName, $"{ccaId++}");

            checkConstraints.Add(new DmCheckConstraint(schemaName, tableName, null, constraintName, cca.Expression));
        }

        var ucas = type.GetCustomAttributes<DmUniqueConstraintAttribute>();
        foreach (var uca in ucas)
        {
            if (uca.Columns == null || uca.Columns.Length == 0)
            {
                continue;
            }

            var constraintName = !string.IsNullOrWhiteSpace(uca.ConstraintName)
                ? uca.ConstraintName
                : DbProviderUtils.GenerateUniqueConstraintName(tableName, uca.Columns);

            uniqueConstraints.Add(
                new DmUniqueConstraint(
                    schemaName,
                    tableName,
                    constraintName,
                    [.. uca.Columns.Select(c => new DmOrderedColumn(c))]
                )
            );

            if (uca.Columns.Length == 1)
            {
                var column = columns.FirstOrDefault(c =>
                    c.ColumnName.Equals(uca.Columns[0], StringComparison.OrdinalIgnoreCase)
                );
                if (column != null)
                {
                    column.IsUnique = true;
                }
            }
        }

        var cias = type.GetCustomAttributes<DmIndexAttribute>();
        foreach (var cia in cias)
        {
            if (cia.Columns == null || cia.Columns.Length == 0)
            {
                // If no columns are specified, skip this index
                // This is to prevent creating an index with no columns, which is not valid
                continue;
            }

            var indexName = !string.IsNullOrWhiteSpace(cia.IndexName)
                ? cia.IndexName
                : DbProviderUtils.GenerateIndexName(tableName, cia.Columns);

            indexes.Add(
                new DmIndex(
                    schemaName,
                    tableName,
                    indexName,
                    [.. cia.Columns.Select(c => new DmOrderedColumn(c))],
                    isUnique: cia.IsUnique
                )
            );

            if (cia.Columns.Length == 1)
            {
                var column = columns.FirstOrDefault(c =>
                    c.ColumnName.Equals(cia.Columns[0], StringComparison.OrdinalIgnoreCase)
                );
                if (column != null)
                {
                    column.IsIndexed = true;
                    if (cia.IsUnique)
                    {
                        column.IsUnique = true;
                    }
                }
            }
        }

        var cfkas = type.GetCustomAttributes<DmForeignKeyConstraintAttribute>();
        foreach (var cfk in cfkas)
        {
            var referencedTableName = cfk.ReferencedTableName;
            if (string.IsNullOrWhiteSpace(referencedTableName) && cfk.ReferencedType != null)
            {
                // Populate the referenced table name from the referenced type
                var referencedTypeInfo = GetTableName(cfk.ReferencedType, ignoreCache: true);
                referencedTableName = referencedTypeInfo.tableName;
            }

            if (
                cfk.SourceColumnNames == null
                || cfk.SourceColumnNames.Length == 0
                || string.IsNullOrWhiteSpace(referencedTableName)
                || cfk.ReferencedColumnNames == null
                || cfk.ReferencedColumnNames.Length == 0
                || cfk.SourceColumnNames.Length != cfk.ReferencedColumnNames.Length
            )
            {
                continue;
            }

            var constraintName = !string.IsNullOrWhiteSpace(cfk.ConstraintName)
                ? cfk.ConstraintName
                : DbProviderUtils.GenerateForeignKeyConstraintName(
                    tableName,
                    cfk.SourceColumnNames,
                    referencedTableName,
                    cfk.ReferencedColumnNames
                );

            var foreignKeyConstraint = new DmForeignKeyConstraint(
                schemaName,
                tableName,
                constraintName,
                [.. cfk.SourceColumnNames.Select(c => new DmOrderedColumn(c))],
                referencedTableName,
                [.. cfk.ReferencedColumnNames.Select(c => new DmOrderedColumn(c))],
                cfk.OnDelete,
                cfk.OnUpdate
            );

            foreignKeyConstraints.Add(foreignKeyConstraint);

            for (var i = 0; i < cfk.SourceColumnNames.Length; i++)
            {
                var sc = cfk.SourceColumnNames[i];
                var column = columns.FirstOrDefault(c => c.ColumnName.Equals(sc, StringComparison.OrdinalIgnoreCase));
                if (column != null)
                {
                    column.IsForeignKey = true;
                    column.ReferencedTableName = referencedTableName;
                    column.ReferencedColumnName = cfk.ReferencedColumnNames[i];
                    column.OnDelete = cfk.OnDelete;
                    column.OnUpdate = cfk.OnUpdate;
                }
            }
        }

        var table = new DmTable(
            schemaName,
            tableName,
            [.. columns],
            primaryKey,
            [.. checkConstraints],
            [.. defaultConstraints],
            [.. uniqueConstraints],
            [.. foreignKeyConstraints],
            [.. indexes]
        );
        return table;
    }
}
