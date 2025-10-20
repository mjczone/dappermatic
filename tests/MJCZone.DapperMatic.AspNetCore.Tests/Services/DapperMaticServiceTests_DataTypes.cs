// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using FluentAssertions;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Services;

public partial class DapperMaticServiceTests
{
    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite)]
    public async Task Can_manage_data_types_Async(string datasourceId)
    {
        using var factory = GetDefaultWebApplicationFactory();
        var service = GetDapperMaticService(factory);

        // Act - Get data types
        var context = OperationIdentifiers.ForDataTypeGet(datasourceId, includeCustomTypes: false);
        var result = await service.GetDatasourceDataTypesAsync(context, datasourceId, includeCustomTypes: true);
        result.providerName.Should().NotBeNullOrWhiteSpace();
        result.dataTypes.Should().NotBeEmpty();
        result.dataTypes.Should().HaveCountGreaterThan(10);

        // Each provider should have at least some common types
        result.dataTypes.Should().Contain(dt => dt.IsCommon);

        Log.WriteLine("Categories:");
        var categories = result.dataTypes.Select(dt => dt.Category).Distinct().ToList();
        foreach (var category in categories)
        {
            Log.WriteLine($"- {category}");
        }

        Log.WriteLine($"Data Types ({result.dataTypes.Count} total):");
        foreach (var dt in result.dataTypes)
        {
            Log.WriteLine(
                $"- {dt.DataType} ({dt.Category}): {dt.Description} (Aliases: {string.Join(", ", dt.Aliases ?? [])})"
            );
            if (dt.SupportsLength)
            {
                Log.WriteLine($"    - Length: Default={dt.DefaultLength}, Min={dt.MinLength}, Max={dt.MaxLength}");
            }
            if (dt.SupportsPrecision)
            {
                Log.WriteLine(
                    $"    - Precision: Default={dt.DefaultPrecision}, Min={dt.MinPrecision}, Max={dt.MaxPrecision}"
                );
            }
            if (dt.SupportsScale)
            {
                Log.WriteLine($"    - Scale: Default={dt.DefaultScale}, Min={dt.MinScale}, Max={dt.MaxScale}");
            }
        }

        switch (datasourceId)
        {
            case TestcontainersAssemblyFixture.DatasourceId_SqlServer:
                ValidateSqlServerDataTypes(result.dataTypes);
                break;
            case TestcontainersAssemblyFixture.DatasourceId_PostgreSql:
                ValidatePostgreSqlDataTypes(result.dataTypes);
                break;
            case TestcontainersAssemblyFixture.DatasourceId_MySql:
                ValidateMySqlDataTypes(result.dataTypes);
                break;
            case TestcontainersAssemblyFixture.DatasourceId_Sqlite:
                ValidateSqliteDataTypes(result.dataTypes);
                break;
        }
    }

    private void ValidateSqlServerDataTypes(List<DataTypeInfo> dataTypes)
    {
        var categories = dataTypes.Select(dt => dt.Category).Distinct().ToList();

        // Expected SqlServer categories
        var expectedCategories = new[]
        {
            DataTypeCategory.Integer,
            DataTypeCategory.Decimal,
            DataTypeCategory.Money,
            DataTypeCategory.Text,
            DataTypeCategory.DateTime,
            DataTypeCategory.Binary,
            // DataTypeCategory.Boolean,
            // DataTypeCategory.Json,
            DataTypeCategory.Xml,
            DataTypeCategory.Spatial,
            // DataTypeCategory.Array,
            // DataTypeCategory.Range,
            // DataTypeCategory.Network,
            DataTypeCategory.Identifier,
            DataTypeCategory.Other,
            // DataTypeCategory.Custom,
        };
        categories.Should().BeSubsetOf(expectedCategories);

        foreach (var category in expectedCategories)
        {
            categories.Should().Contain(category);
        }

        var expectedDataTypes = new[]
        {
            "bigint",
            "bit",
            "int",
            "smallint",
            "tinyint",
            "decimal",
            "float",
            "numeric",
            "real",
            "money",
            "smallmoney",
            "char",
            "nchar",
            "ntext",
            "nvarchar",
            "nvarchar(max)",
            "text",
            "varchar",
            "varchar(max)",
            "date",
            "datetime",
            "datetime2",
            "datetimeoffset",
            "smalldatetime",
            "time",
            "binary",
            "image",
            "varbinary",
            "varbinary(max)",
            // "json",
            "xml",
            "geography",
            "geometry",
            "uniqueidentifier",
            // "cursor", // Not a data type, it's a programming construct
            "hierarchyid",
            "rowversion",
            "sql_variant",
            // "table", // Not a column data type, used for table variables
            "timestamp",
        };

        // Create a list of available type names for clearer error messages
        var availableTypeNames = dataTypes.Select(dt => dt.DataType).ToList();

        foreach (var type in expectedDataTypes)
        {
            availableTypeNames
                .Should()
                .Contain(type, $"SQL Server should have data type '{type}'");
        }
    }

    private void ValidatePostgreSqlDataTypes(List<DataTypeInfo> dataTypes)
    {
        var categories = dataTypes.Select(dt => dt.Category).Distinct().ToList();

        // Expected PostgreSQL categories
        var expectedCategories = new[]
        {
            DataTypeCategory.Integer,
            DataTypeCategory.Decimal,
            DataTypeCategory.Money,
            DataTypeCategory.Text,
            DataTypeCategory.DateTime,
            DataTypeCategory.Binary,
            DataTypeCategory.Boolean,
            DataTypeCategory.Json,
            DataTypeCategory.Xml,
            DataTypeCategory.Spatial,
            DataTypeCategory.Array,
            DataTypeCategory.Range,
            DataTypeCategory.Network,
            DataTypeCategory.Identifier,
            DataTypeCategory.Other,
            // DataTypeCategory.Custom,
        };
        categories.Should().BeSubsetOf(expectedCategories);

        foreach (var category in expectedCategories)
        {
            categories.Should().Contain(category);
        }

        var expectedDataTypes = new[]
        {
            "bigint",
            "bigserial",
            "integer",
            "serial",
            "smallint",
            "smallserial",
            "double precision",
            "numeric",
            "real",
            "money",
            "bit",
            "bit varying",
            "character",
            "character varying",
            "text",
            "date",
            "time",
            "time with time zone",
            "timestamp",
            "timestamp with time zone",
            "bytea",
            "boolean",
            "json",
            "jsonb",
            "xml",
            "box",
            "circle",
            "geography",
            "geometry",
            "line",
            "lseg",
            "path",
            "point",
            "polygon",
            "integer[]",
            "numeric[]",
            "text[]",
            "daterange",
            "int4range",
            "int8range",
            "numrange",
            "tsrange",
            "tstzrange",
            "cidr",
            "inet",
            "macaddr",
            "macaddr8",
            "uuid",
            "hstore",
            "ltree",
            "oid",
            "regclass",
            "regconfig",
            "regdictionary",
            "regoper",
            "regoperator",
            "regproc",
            "regprocedure",
            "regtype",
            "tsquery",
            "tsvector",
        };

        // Create a list of available type names for clearer error messages
        var availableTypeNames = dataTypes.Select(dt => dt.DataType).ToList();

        foreach (var type in expectedDataTypes)
        {
            availableTypeNames
                .Should()
                .Contain(type, $"PostgreSQL should have data type '{type}'");
        }
    }

    private void ValidateMySqlDataTypes(List<DataTypeInfo> dataTypes)
    {
        var categories = dataTypes.Select(dt => dt.Category).Distinct().ToList();

        // Expected MySQL categories
        var expectedCategories = new[]
        {
            DataTypeCategory.Integer,
            DataTypeCategory.Decimal,
            // DataTypeCategory.Money,
            DataTypeCategory.Text,
            DataTypeCategory.DateTime,
            DataTypeCategory.Binary,
            DataTypeCategory.Boolean,
            DataTypeCategory.Json,
            // DataTypeCategory.Xml,
            DataTypeCategory.Spatial,
            // DataTypeCategory.Array,
            // DataTypeCategory.Range,
            // DataTypeCategory.Network,
            // DataTypeCategory.Identifier,
            DataTypeCategory.Other,
            // DataTypeCategory.Custom,
        };
        categories.Should().BeSubsetOf(expectedCategories);

        foreach (var category in expectedCategories)
        {
            categories.Should().Contain(category);
        }

        var expectedDataTypes = new[]
        {
            "bigint",
            "int",
            "mediumint",
            "smallint",
            "tinyint",
            "decimal",
            "double",
            "float",
            "binary",
            "bit",
            "char",
            "longtext",
            "mediumtext",
            "text",
            "tinytext",
            "varbinary",
            "varchar",
            "date",
            "datetime",
            "time",
            "timestamp",
            "year",
            "blob",
            "longblob",
            "mediumblob",
            "tinyblob",
            "boolean",
            "json",
            "geometry",
            "geometrycollection",
        };

        // Create a list of available type names for clearer error messages
        var availableTypeNames = dataTypes.Select(dt => dt.DataType).ToList();

        foreach (var type in expectedDataTypes)
        {
            availableTypeNames
                .Should()
                .Contain(type, $"MySQL should have data type '{type}'");
        }
    }

    private void ValidateSqliteDataTypes(List<DataTypeInfo> dataTypes)
    {
        var categories = dataTypes.Select(dt => dt.Category).Distinct().ToList();

        // Expected SQLite categories
        var expectedCategories = new[]
        {
            DataTypeCategory.Integer,
            DataTypeCategory.Decimal,
            // DataTypeCategory.Money,
            DataTypeCategory.Text,
            DataTypeCategory.DateTime,
            DataTypeCategory.Binary,
            DataTypeCategory.Boolean,
            // DataTypeCategory.Json,
            // DataTypeCategory.Xml,
            // DataTypeCategory.Spatial,
            // DataTypeCategory.Array,
            // DataTypeCategory.Range,
            // DataTypeCategory.Network,
            // DataTypeCategory.Identifier,
            // DataTypeCategory.Other,
            // DataTypeCategory.Custom,
        };
        categories.Should().BeSubsetOf(expectedCategories);

        foreach (var category in expectedCategories)
        {
            categories.Should().Contain(category);
        }

        var expectedDataTypes = new[]
        {
            "integer",
            "decimal",
            "numeric",
            "real",
            "char",
            "text",
            "varchar",
            "date",
            "datetime",
            "timestamp",
            "blob",
            "boolean",
            // "json", // SQLite stores JSON as text, not a separate data type
        };

        // Create a list of available type names for clearer error messages
        var availableTypeNames = dataTypes.Select(dt => dt.DataType).ToList();

        foreach (var type in expectedDataTypes)
        {
            availableTypeNames
                .Should()
                .Contain(type, $"SQLite should have data type '{type}'");
        }
    }
}
