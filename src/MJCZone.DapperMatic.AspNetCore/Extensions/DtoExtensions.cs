// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.AspNetCore.Extensions;

/// <summary>
/// Extension methods for converting internal models to DTOs.
/// </summary>
internal static class DtoExtensions
{
    /// <summary>
    /// Converts a DataTypeInfo to a DataTypeDto.
    /// </summary>
    /// <param name="dataTypeInfo">The internal data type info.</param>
    /// <returns>The data type DTO.</returns>
    public static DataTypeDto ToDataTypeDto(this DataTypeInfo dataTypeInfo)
    {
        return new DataTypeDto
        {
            DataType = dataTypeInfo.DataType,
            Aliases = dataTypeInfo.Aliases ?? [],
            IsCommon = dataTypeInfo.IsCommon,
            IsCustom = dataTypeInfo.IsCustom,
            SupportsLength = dataTypeInfo.SupportsLength,
            MinLength = dataTypeInfo.MinLength,
            MaxLength = dataTypeInfo.MaxLength,
            DefaultLength = dataTypeInfo.DefaultLength,
            SupportsPrecision = dataTypeInfo.SupportsPrecision,
            MinPrecision = dataTypeInfo.MinPrecision,
            MaxPrecision = dataTypeInfo.MaxPrecision,
            DefaultPrecision = dataTypeInfo.DefaultPrecision,
            SupportsScale = dataTypeInfo.SupportsScale,
            MinScale = dataTypeInfo.MinScale,
            MaxScale = dataTypeInfo.MaxScale,
            DefaultScale = dataTypeInfo.DefaultScale,
            Category = dataTypeInfo.Category.ToString(),
            Description = dataTypeInfo.Description,
            Examples = dataTypeInfo.Examples,
        };
    }

    /// <summary>
    /// Converts a collection of DataTypeInfo to DataTypeDto.
    /// </summary>
    /// <param name="dataTypeInfos">The collection of internal data type infos.</param>
    /// <returns>A collection of data type DTOs.</returns>
    public static IEnumerable<DataTypeDto> ToDataTypeDtos(
        this IEnumerable<DataTypeInfo> dataTypeInfos
    )
    {
        return dataTypeInfos.Select(ToDataTypeDto);
    }

    /// <summary>
    /// Converts a .NET Type to a friendly type name string.
    /// </summary>
    /// <param name="type">The .NET type.</param>
    /// <returns>A friendly type name string.</returns>
    public static string ToFriendlyTypeName(this Type type)
    {
        if (type.IsGenericType)
        {
            var genericTypeName = type.Name.Split('`')[0];
            var genericArgs = string.Join(
                ", ",
                type.GetGenericArguments().Select(ToFriendlyTypeName)
            );
            return $"{genericTypeName}<{genericArgs}>";
        }

        return type.Name switch
        {
            "Int32" => "int",
            "Int64" => "long",
            "Int16" => "short",
            "Byte" => "byte",
            "Boolean" => "bool",
            "Single" => "float",
            "Double" => "double",
            "Decimal" => "decimal",
            "String" => "string",
            "Object" => "object",
            _ => type.Name,
        };
    }

    /// <summary>
    /// Converts a DmColumn to a ColumnDto.
    /// </summary>
    /// <param name="column">The internal column.</param>
    /// <returns>The column DTO.</returns>
    public static ColumnDto ToColumnDto(this DmColumn column)
    {
        // Get the provider data type for the current connection
        var providerDataType = column.ProviderDataTypes.Values.FirstOrDefault() ?? "UNKNOWN";

        return new ColumnDto
        {
            ColumnName = column.ColumnName,
            DotnetTypeName = column.DotnetType?.ToFriendlyTypeName(),
            ProviderDataType = providerDataType,
            Length = column.Length,
            Precision = column.Precision,
            Scale = column.Scale,
            CheckExpression = column.CheckExpression,
            DefaultExpression = column.DefaultExpression,
            IsNullable = column.IsNullable,
            IsPrimaryKey = column.IsPrimaryKey,
            IsAutoIncrement = column.IsAutoIncrement,
            IsUnique = column.IsUnique,
            IsUnicode = column.IsUnicode,
            IsFixedLength = column.IsFixedLength,
            IsIndexed = column.IsIndexed,
            IsForeignKey = column.IsForeignKey,
            ReferencedTableName = column.ReferencedTableName,
            ReferencedColumnName = column.ReferencedColumnName,
        };
    }

    /// <summary>
    /// Converts a collection of DmColumn to ColumnDto.
    /// </summary>
    /// <param name="columns">The collection of internal columns.</param>
    /// <returns>A collection of column DTOs.</returns>
    public static IEnumerable<ColumnDto> ToColumnDtos(this IEnumerable<DmColumn> columns)
    {
        return columns.Select(ToColumnDto);
    }

    /// <summary>
    /// Converts a DmIndex to an IndexDto.
    /// </summary>
    /// <param name="index">The internal index.</param>
    /// <returns>The index DTO.</returns>
    public static IndexDto ToIndexDto(this DmIndex index)
    {
        return new IndexDto
        {
            IndexName = index.IndexName,
            ColumnNames = index.Columns.Select(c => c.ColumnName).ToList(),
            IsUnique = index.IsUnique,
            IsClustered = false, // DmIndex doesn't have IsClustered property, default to false
        };
    }

    /// <summary>
    /// Converts a collection of DmIndex to IndexDto.
    /// </summary>
    /// <param name="indexes">The collection of internal indexes.</param>
    /// <returns>A collection of index DTOs.</returns>
    public static IEnumerable<IndexDto> ToIndexDtos(this IEnumerable<DmIndex> indexes)
    {
        return indexes.Select(ToIndexDto);
    }

    /// <summary>
    /// Converts a DmPrimaryKeyConstraint to a PrimaryKeyConstraintDto.
    /// </summary>
    /// <param name="primaryKey">The internal primary key constraint.</param>
    /// <returns>The primary key constraint DTO.</returns>
    public static PrimaryKeyConstraintDto ToPrimaryKeyConstraintDto(
        this DmPrimaryKeyConstraint primaryKey
    )
    {
        return new PrimaryKeyConstraintDto
        {
            ConstraintName = primaryKey.ConstraintName,
            ColumnNames = primaryKey.Columns.Select(c => c.ColumnName).ToList(),
        };
    }

    /// <summary>
    /// Converts a DmForeignKeyConstraint to a ForeignKeyConstraintDto.
    /// </summary>
    /// <param name="foreignKey">The internal foreign key constraint.</param>
    /// <returns>The foreign key constraint DTO.</returns>
    public static ForeignKeyConstraintDto ToForeignKeyConstraintDto(
        this DmForeignKeyConstraint foreignKey
    )
    {
        return new ForeignKeyConstraintDto
        {
            ConstraintName = foreignKey.ConstraintName,
            ColumnNames = foreignKey.SourceColumns.Select(c => c.ColumnName).ToList(),
            ReferencedTableName = foreignKey.ReferencedTableName,
            ReferencedColumnNames = foreignKey.ReferencedColumns.Select(c => c.ColumnName).ToList(),
            OnDelete = foreignKey.OnDelete.ToString(),
            OnUpdate = foreignKey.OnUpdate.ToString(),
        };
    }

    /// <summary>
    /// Converts a collection of DmForeignKeyConstraint to ForeignKeyConstraintDto.
    /// </summary>
    /// <param name="foreignKeys">The collection of internal foreign key constraints.</param>
    /// <returns>A collection of foreign key constraint DTOs.</returns>
    public static IEnumerable<ForeignKeyConstraintDto> ToForeignKeyConstraintDtos(
        this IEnumerable<DmForeignKeyConstraint> foreignKeys
    )
    {
        return foreignKeys.Select(ToForeignKeyConstraintDto);
    }

    /// <summary>
    /// Converts a DmCheckConstraint to a CheckConstraintDto.
    /// </summary>
    /// <param name="checkConstraint">The internal check constraint.</param>
    /// <returns>The check constraint DTO.</returns>
    public static CheckConstraintDto ToCheckConstraintDto(this DmCheckConstraint checkConstraint)
    {
        return new CheckConstraintDto
        {
            ConstraintName = checkConstraint.ConstraintName,
            ColumnName = checkConstraint.ColumnName,
            CheckExpression = checkConstraint.Expression,
        };
    }

    /// <summary>
    /// Converts a collection of DmCheckConstraint to CheckConstraintDto.
    /// </summary>
    /// <param name="checkConstraints">The collection of internal check constraints.</param>
    /// <returns>A collection of check constraint DTOs.</returns>
    public static IEnumerable<CheckConstraintDto> ToCheckConstraintDtos(
        this IEnumerable<DmCheckConstraint> checkConstraints
    )
    {
        return checkConstraints.Select(ToCheckConstraintDto);
    }

    /// <summary>
    /// Converts a DmUniqueConstraint to a UniqueConstraintDto.
    /// </summary>
    /// <param name="uniqueConstraint">The internal unique constraint.</param>
    /// <returns>The unique constraint DTO.</returns>
    public static UniqueConstraintDto ToUniqueConstraintDto(
        this DmUniqueConstraint uniqueConstraint
    )
    {
        return new UniqueConstraintDto
        {
            ConstraintName = uniqueConstraint.ConstraintName,
            ColumnNames = uniqueConstraint.Columns.Select(c => c.ColumnName).ToList(),
        };
    }

    /// <summary>
    /// Converts a collection of DmUniqueConstraint to UniqueConstraintDto.
    /// </summary>
    /// <param name="uniqueConstraints">The collection of internal unique constraints.</param>
    /// <returns>A collection of unique constraint DTOs.</returns>
    public static IEnumerable<UniqueConstraintDto> ToUniqueConstraintDtos(
        this IEnumerable<DmUniqueConstraint> uniqueConstraints
    )
    {
        return uniqueConstraints.Select(ToUniqueConstraintDto);
    }

    /// <summary>
    /// Converts a DmDefaultConstraint to a DefaultConstraintDto.
    /// </summary>
    /// <param name="defaultConstraint">The internal default constraint.</param>
    /// <returns>The default constraint DTO.</returns>
    public static DefaultConstraintDto ToDefaultConstraintDto(
        this DmDefaultConstraint defaultConstraint
    )
    {
        return new DefaultConstraintDto
        {
            ConstraintName = defaultConstraint.ConstraintName,
            ColumnName = defaultConstraint.ColumnName,
            DefaultExpression = defaultConstraint.Expression,
        };
    }

    /// <summary>
    /// Converts a collection of DmDefaultConstraint to DefaultConstraintDto.
    /// </summary>
    /// <param name="defaultConstraints">The collection of internal default constraints.</param>
    /// <returns>A collection of default constraint DTOs.</returns>
    public static IEnumerable<DefaultConstraintDto> ToDefaultConstraintDtos(
        this IEnumerable<DmDefaultConstraint> defaultConstraints
    )
    {
        return defaultConstraints.Select(ToDefaultConstraintDto);
    }

    /// <summary>
    /// Converts a DmView to a ViewDto.
    /// </summary>
    /// <param name="view">The internal view.</param>
    /// <returns>The view DTO.</returns>
    public static ViewDto ToViewDto(this DmView view)
    {
        return new ViewDto
        {
            SchemaName = view.SchemaName,
            ViewName = view.ViewName,
            Definition = view.Definition,
        };
    }

    /// <summary>
    /// Converts a collection of DmView to ViewDto.
    /// </summary>
    /// <param name="views">The collection of internal views.</param>
    /// <returns>A collection of view DTOs.</returns>
    public static IEnumerable<ViewDto> ToViewDtos(this IEnumerable<DmView> views)
    {
        return views.Select(ToViewDto);
    }

    /// <summary>
    /// Converts a DmTable to a TableDto.
    /// </summary>
    /// <param name="table">The internal table.</param>
    /// <param name="includeColumns">Whether to include column information.</param>
    /// <param name="includeIndexes">Whether to include index information.</param>
    /// <param name="includeConstraints">Whether to include constraint information.</param>
    /// <returns>The table DTO.</returns>
    public static TableDto ToTableDto(
        this DmTable table,
        bool includeColumns = true,
        bool includeIndexes = true,
        bool includeConstraints = true
    )
    {
        var tableDto = new TableDto { TableName = table.TableName };

        if (includeColumns)
        {
            tableDto.Columns = [.. table.Columns.ToColumnDtos()];
        }

        if (includeConstraints)
        {
            if (table.PrimaryKeyConstraint != null)
            {
                tableDto.PrimaryKeyConstraint = new PrimaryKeyConstraintDto
                {
                    ConstraintName = table.PrimaryKeyConstraint.ConstraintName,
                    ColumnNames = table
                        .PrimaryKeyConstraint.Columns.Select(c => c.ColumnName)
                        .ToList(),
                };
            }

            tableDto.CheckConstraints = table
                .CheckConstraints.Select(cc => new CheckConstraintDto
                {
                    ConstraintName = cc.ConstraintName,
                    ColumnName = cc.ColumnName,
                    CheckExpression = cc.Expression,
                })
                .ToList();

            tableDto.DefaultConstraints = table
                .DefaultConstraints.Select(dc => new DefaultConstraintDto
                {
                    ConstraintName = dc.ConstraintName,
                    ColumnName = dc.ColumnName,
                    DefaultExpression = dc.Expression,
                })
                .ToList();

            tableDto.UniqueConstraints = table
                .UniqueConstraints.Select(uc => new UniqueConstraintDto
                {
                    ConstraintName = uc.ConstraintName,
                    ColumnNames = uc.Columns.Select(c => c.ColumnName).ToList(),
                })
                .ToList();

            tableDto.ForeignKeyConstraints = table
                .ForeignKeyConstraints.Select(fk => new ForeignKeyConstraintDto
                {
                    ConstraintName = fk.ConstraintName,
                    ColumnNames = fk.SourceColumns.Select(c => c.ColumnName).ToList(),
                    ReferencedTableName = fk.ReferencedTableName,
                    ReferencedColumnNames = fk.ReferencedColumns.Select(c => c.ColumnName).ToList(),
                    OnDelete = fk.OnDelete.ToString(),
                    OnUpdate = fk.OnUpdate.ToString(),
                })
                .ToList();
        }

        if (includeIndexes)
        {
            tableDto.Indexes = table
                .Indexes.Select(idx => new IndexDto
                {
                    IndexName = idx.IndexName,
                    ColumnNames = idx.Columns.Select(c => c.ColumnName).ToList(),
                    IsUnique = idx.IsUnique,
                    IsClustered = false, // DmIndex doesn't have IsClustered property, default to false
                })
                .ToList();
        }

        return tableDto;
    }

    /// <summary>
    /// Converts a collection of DmTable to TableDto.
    /// </summary>
    /// <param name="tables">The collection of internal tables.</param>
    /// <param name="includeColumns">Whether to include column information.</param>
    /// <param name="includeIndexes">Whether to include index information.</param>
    /// <param name="includeConstraints">Whether to include constraint information.</param>
    /// <returns>A collection of table DTOs.</returns>
    public static IEnumerable<TableDto> ToTableDtos(
        this IEnumerable<DmTable> tables,
        bool includeColumns = true,
        bool includeIndexes = true,
        bool includeConstraints = true
    )
    {
        return tables.Select(t => t.ToTableDto(includeColumns, includeIndexes, includeConstraints));
    }

    /// <summary>
    /// Converts a ViewDto to a DmView.
    /// </summary>
    /// <param name="viewDto">The view DTO.</param>
    /// <returns>The internal view.</returns>
    /// <exception cref="ArgumentException">Thrown when required properties are null or empty.</exception>
    public static DmView ToDmView(this ViewDto viewDto)
    {
        if (string.IsNullOrWhiteSpace(viewDto.ViewName))
        {
            throw new ArgumentException("ViewName is required", nameof(viewDto));
        }

        if (string.IsNullOrWhiteSpace(viewDto.Definition))
        {
            throw new ArgumentException("Definition is required", nameof(viewDto));
        }

        return new DmView(viewDto.SchemaName, viewDto.ViewName, viewDto.Definition);
    }

    /// <summary>
    /// Converts a TableDto to a DmTable.
    /// </summary>
    /// <param name="tableDto">The table DTO.</param>
    /// <returns>The internal table.</returns>
    /// <exception cref="ArgumentException">Thrown when required properties are null or empty.</exception>
    public static DmTable ToDmTable(this TableDto tableDto)
    {
        if (string.IsNullOrWhiteSpace(tableDto.TableName))
        {
            throw new ArgumentException("TableName is required", nameof(tableDto));
        }

        // Convert columns
        var columns = tableDto.Columns?.Select(c => c.ToDmColumn(tableDto.SchemaName, tableDto.TableName)).ToArray() ?? [];

        // Convert primary key constraint
        DmPrimaryKeyConstraint? primaryKey = null;
        if (tableDto.PrimaryKeyConstraint != null)
        {
            primaryKey = tableDto.PrimaryKeyConstraint.ToDmPrimaryKeyConstraint(
                tableDto.SchemaName,
                tableDto.TableName
            );
        }

        // Convert constraints
        var checkConstraints = tableDto
            .CheckConstraints?.Select(c =>
                c.ToDmCheckConstraint(tableDto.SchemaName, tableDto.TableName)
            )
            .ToArray();
        var defaultConstraints = tableDto
            .DefaultConstraints?.Select(c =>
                c.ToDmDefaultConstraint(tableDto.SchemaName, tableDto.TableName)
            )
            .ToArray();
        var uniqueConstraints = tableDto
            .UniqueConstraints?.Select(c =>
                c.ToDmUniqueConstraint(tableDto.SchemaName, tableDto.TableName)
            )
            .ToArray();
        var foreignKeyConstraints = tableDto
            .ForeignKeyConstraints?.Select(c =>
                c.ToDmForeignKeyConstraint(tableDto.SchemaName, tableDto.TableName)
            )
            .ToArray();

        // Convert indexes
        var indexes = tableDto
            .Indexes?.Select(i => i.ToDmIndex(tableDto.SchemaName, tableDto.TableName))
            .ToArray();

        return new DmTable(
            tableDto.SchemaName,
            tableDto.TableName,
            columns,
            primaryKey,
            checkConstraints,
            defaultConstraints,
            uniqueConstraints,
            foreignKeyConstraints,
            indexes
        );
    }

    /// <summary>
    /// Converts a ColumnDto to a DmColumn.
    /// </summary>
    /// <param name="columnDto">The column DTO.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="tableName">The table name.</param>
    /// <returns>The internal column.</returns>
    /// <exception cref="ArgumentException">Thrown when required properties are null or empty.</exception>
    public static DmColumn ToDmColumn(
        this ColumnDto columnDto,
        string? schemaName,
        string tableName
    )
    {
        if (string.IsNullOrWhiteSpace(columnDto.ColumnName))
        {
            throw new ArgumentException("ColumnName is required", nameof(columnDto));
        }

        if (string.IsNullOrWhiteSpace(columnDto.ProviderDataType))
        {
            throw new ArgumentException("ProviderDataType is required", nameof(columnDto));
        }

        // Create basic column with provider data type
        var providerDataTypes = new Dictionary<DbProviderType, string>
        {
            // We'll set all provider types - as we don't know which the dto impacts
            { DbProviderType.SqlServer, columnDto.ProviderDataType },
            { DbProviderType.PostgreSql, columnDto.ProviderDataType },
            { DbProviderType.MySql, columnDto.ProviderDataType },
            { DbProviderType.Sqlite, columnDto.ProviderDataType },
        };

        return new DmColumn(
            schemaName,
            tableName,
            columnDto.ColumnName,
            columnDto.DotnetTypeName != null ? Type.GetType(columnDto.DotnetTypeName) : null,
            providerDataTypes: providerDataTypes,
            length: columnDto.Length,
            precision: columnDto.Precision,
            scale: columnDto.Scale,
            isPrimaryKey: columnDto.IsPrimaryKey,
            isAutoIncrement: columnDto.IsAutoIncrement,
            isNullable: columnDto.IsNullable,
            isUnique: columnDto.IsUnique,
            isUnicode: columnDto.IsUnicode,
            isIndexed: columnDto.IsIndexed,
            defaultExpression: columnDto.DefaultExpression,
            checkExpression: columnDto.CheckExpression
        );
    }

    /// <summary>
    /// Converts a PrimaryKeyConstraintDto to a DmPrimaryKeyConstraint.
    /// </summary>
    /// <param name="pkDto">The primary key constraint DTO.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="tableName">The table name.</param>
    /// <returns>The internal primary key constraint.</returns>
    public static DmPrimaryKeyConstraint ToDmPrimaryKeyConstraint(
        this PrimaryKeyConstraintDto pkDto,
        string? schemaName,
        string tableName
    )
    {
        var columns = pkDto.ColumnNames.Select(name => DmOrderedColumn.Parse(name)).ToArray();
        return new DmPrimaryKeyConstraint(
            schemaName,
            tableName,
            pkDto.ConstraintName ?? string.Empty,
            columns
        );
    }

    /// <summary>
    /// Converts a CheckConstraintDto to a DmCheckConstraint.
    /// </summary>
    /// <param name="checkDto">The check constraint DTO.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="tableName">The table name.</param>
    /// <returns>The internal check constraint.</returns>
    public static DmCheckConstraint ToDmCheckConstraint(
        this CheckConstraintDto checkDto,
        string? schemaName,
        string tableName
    )
    {
        return new DmCheckConstraint(
            schemaName,
            tableName,
            checkDto.ColumnName,
            checkDto.ConstraintName ?? string.Empty,
            checkDto.CheckExpression ?? string.Empty
        );
    }

    /// <summary>
    /// Converts a DefaultConstraintDto to a DmDefaultConstraint.
    /// </summary>
    /// <param name="defaultDto">The default constraint DTO.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="tableName">The table name.</param>
    /// <returns>The internal default constraint.</returns>
    public static DmDefaultConstraint ToDmDefaultConstraint(
        this DefaultConstraintDto defaultDto,
        string? schemaName,
        string tableName
    )
    {
        return new DmDefaultConstraint(
            schemaName,
            tableName,
            defaultDto.ColumnName ?? string.Empty,
            defaultDto.ConstraintName ?? string.Empty,
            defaultDto.DefaultExpression ?? string.Empty
        );
    }

    /// <summary>
    /// Converts a UniqueConstraintDto to a DmUniqueConstraint.
    /// </summary>
    /// <param name="uniqueDto">The unique constraint DTO.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="tableName">The table name.</param>
    /// /// <returns>The internal unique constraint.</returns>
    public static DmUniqueConstraint ToDmUniqueConstraint(
        this UniqueConstraintDto uniqueDto,
        string? schemaName,
        string tableName
    )
    {
        var columns = uniqueDto.ColumnNames.Select(name => DmOrderedColumn.Parse(name)).ToArray();
        return new DmUniqueConstraint(
            schemaName,
            tableName,
            uniqueDto.ConstraintName ?? string.Empty,
            columns
        );
    }

    /// <summary>
    /// Converts a ForeignKeyConstraintDto to a DmForeignKeyConstraint.
    /// </summary>
    /// <param name="fkDto">The foreign key constraint DTO.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="tableName">The table name.</param>
    /// <returns>The internal foreign key constraint.</returns>
    public static DmForeignKeyConstraint ToDmForeignKeyConstraint(
        this ForeignKeyConstraintDto fkDto,
        string? schemaName,
        string tableName
    )
    {
        var sourceColumns = fkDto.ColumnNames.Select(name => DmOrderedColumn.Parse(name)).ToArray();
        var referencedColumns = fkDto
            .ReferencedColumnNames.Select(name => DmOrderedColumn.Parse(name))
            .ToArray();

        var onUpdate = Enum.TryParse<DmForeignKeyAction>(fkDto.OnUpdate, true, out var updateAction)
            ? updateAction
            : DmForeignKeyAction.NoAction;
        var onDelete = Enum.TryParse<DmForeignKeyAction>(fkDto.OnDelete, true, out var deleteAction)
            ? deleteAction
            : DmForeignKeyAction.NoAction;

        return new DmForeignKeyConstraint(
            schemaName,
            tableName,
            fkDto.ConstraintName ?? string.Empty,
            sourceColumns,
            fkDto.ReferencedTableName ?? string.Empty,
            referencedColumns,
            onUpdate,
            onDelete
        );
    }

    /// <summary>
    /// Converts an IndexDto to a DmIndex.
    /// </summary>
    /// <param name="indexDto">The index DTO.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="tableName">The table name.</param>
    /// <returns>The internal index.</returns>
    public static DmIndex ToDmIndex(this IndexDto indexDto, string? schemaName, string tableName)
    {
        var columns = indexDto.ColumnNames.Select(name => DmOrderedColumn.Parse(name)).ToArray();
        return new DmIndex(
            schemaName,
            tableName,
            indexDto.IndexName ?? string.Empty,
            columns,
            isUnique: indexDto.IsUnique
        );
    }
}
