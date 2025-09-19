// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Security;

/// <summary>
/// Utility class for identifying DapperMatic operation tags.
/// </summary>
public static class OperationTags
{
    /// <summary>
    /// Tag for all DapperMatic Datasource-related operations.
    /// </summary>
    public const string Datasources = "DapperMatic Datasources";

    /// <summary>
    /// Tag for all DapperMatic Schema-related operations.
    /// </summary>
    public const string DatasourceDataTypes = "DapperMatic Datasource DataTypes";

    /// <summary>
    /// Tag for all DapperMatic Schema-related operations.
    /// </summary>
    public const string DatasourceSchemas = "DapperMatic Datasource Schemas";

    /// <summary>
    /// Tag for all DapperMatic Table-related operations.
    /// </summary>
    public const string DatasourceTables = "DapperMatic Datasource Tables";

    /// <summary>
    /// Tag for all DapperMatic View-related operations.
    /// </summary>
    public const string DatasourceViews = "DapperMatic Datasource Views";
}
