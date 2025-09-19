// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Security;

/// <summary>
/// Utility class for identifying DapperMatic operation identifiers.
/// </summary>
public static class OperationIdentifiers
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    // Datasource operations
    public const string ListDatasources = "datasources/list";
    public const string GetDatasource = "datasources/get";
    public const string AddDatasource = "datasources/add";
    public const string UpdateDatasource = "datasources/update";
    public const string RemoveDatasource = "datasources/remove";
    public const string TestDatasource = "datasources/test";

    // DataType operations
    public const string ListDataTypes = "datatypes/list";

    // Schema operations
    public const string ListSchemas = "schemas/list";
    public const string GetSchema = "schemas/get";
    public const string CreateSchema = "schemas/create";
    public const string DropSchema = "schemas/drop";
    public const string SchemaExists = "schemas/exists";

    // View operations
    public const string ListViews = "views/list";
    public const string GetView = "views/get";
    public const string CreateView = "views/create";
    public const string UpdateView = "views/update";
    public const string DropView = "views/drop";
    public const string ViewExists = "views/exists";
    public const string QueryView = "views/query";

    // Table operations
    public const string ListTables = "tables/list";
    public const string GetTable = "tables/get";
    public const string CreateTable = "tables/create";
    public const string UpdateTable = "tables/update";
    public const string DropTable = "tables/drop";
    public const string RenameTable = "tables/rename";
    public const string TableExists = "tables/exists";
    public const string QueryTable = "tables/query";

    // Column operations
    public const string ListColumns = "columns/list";
    public const string GetColumn = "columns/get";
    public const string AddColumn = "columns/add";
    public const string UpdateColumn = "columns/update";
    public const string DropColumn = "columns/drop";

    // Index operations
    public const string ListIndexes = "indexes/list";
    public const string GetIndex = "indexes/get";
    public const string CreateIndex = "indexes/create";
    public const string DropIndex = "indexes/drop";

    // Constraint operations
    public const string GetPrimaryKey = "constraints/primarykey/get";
    public const string CreatePrimaryKey = "constraints/primarykey/create";
    public const string DropPrimaryKey = "constraints/primarykey/drop";

    public const string ListForeignKeys = "constraints/foreignkeys/list";
    public const string GetForeignKey = "constraints/foreignkeys/get";
    public const string CreateForeignKey = "constraints/foreignkeys/create";
    public const string DropForeignKey = "constraints/foreignkeys/drop";

    public const string ListCheckConstraints = "constraints/checks/list";
    public const string GetCheckConstraint = "constraints/checks/get";
    public const string CreateCheckConstraint = "constraints/checks/create";
    public const string DropCheckConstraint = "constraints/checks/drop";

    public const string ListUniqueConstraints = "constraints/uniques/list";
    public const string GetUniqueConstraint = "constraints/uniques/get";
    public const string CreateUniqueConstraint = "constraints/uniques/create";
    public const string DropUniqueConstraint = "constraints/uniques/drop";

    public const string ListDefaultConstraints = "constraints/defaults/list";
    public const string GetDefaultConstraint = "constraints/defaults/get";
    public const string CreateDefaultConstraint = "constraints/defaults/create";
    public const string DropDefaultConstraint = "constraints/defaults/drop";

#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    /// <summary>
    /// Determines if the given operation string represents a "get" operation.
    /// </summary>
    /// <param name="operation">The operation string to check.</param>
    /// <returns>True if it is a "get" operation; otherwise, false.</returns>
    public static bool IsGetOperation(string operation) =>
        operation.EndsWith("/get", StringComparison.OrdinalIgnoreCase)
        || operation.EndsWith("/list", StringComparison.OrdinalIgnoreCase);
}
