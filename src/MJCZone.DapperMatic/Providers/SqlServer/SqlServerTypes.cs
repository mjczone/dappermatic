// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace MJCZone.DapperMatic.Providers.SqlServer;

/// <summary>
/// Provides constants for SQL Server data types.
/// </summary>
/// <remarks>
/// See: https://learn.microsoft.com/en-us/sql/t-sql/data-types/data-types-transact-sql?view=sql-server-ver16
/// ...
/// </remarks>
[SuppressMessage(
    "ReSharper",
    "InconsistentNaming",
    Justification = "Constants are named to match SQL Server documentation."
)]
public static class SqlServerTypes
{
    /// <summary>SQL Server bit type (0 or 1).</summary>
    public const string sql_bit = "bit";

    /// <summary>SQL Server tinyint type.</summary>
    public const string sql_tinyint = "tinyint";

    /// <summary>SQL Server smallint type.</summary>
    public const string sql_smallint = "smallint";

    /// <summary>SQL Server int type.</summary>
    public const string sql_int = "int";

    /// <summary>SQL Server bigint type.</summary>
    public const string sql_bigint = "bigint";

    /// <summary>SQL Server float type.</summary>
    public const string sql_float = "float";

    /// <summary>SQL Server real type.</summary>
    public const string sql_real = "real";

    /// <summary>SQL Server decimal type.</summary>
    public const string sql_decimal = "decimal";

    /// <summary>SQL Server numeric type.</summary>
    public const string sql_numeric = "numeric";

    /// <summary>SQL Server money type.</summary>
    public const string sql_money = "money";

    /// <summary>SQL Server smallmoney type.</summary>
    public const string sql_smallmoney = "smallmoney";

    /// <summary>SQL Server date type.</summary>
    public const string sql_date = "date";

    /// <summary>SQL Server datetime type.</summary>
    public const string sql_datetime = "datetime";

    /// <summary>SQL Server smalldatetime type.</summary>
    public const string sql_smalldatetime = "smalldatetime";

    /// <summary>SQL Server datetime2 type.</summary>
    public const string sql_datetime2 = "datetime2";

    /// <summary>SQL Server datetimeoffset type.</summary>
    public const string sql_datetimeoffset = "datetimeoffset";

    /// <summary>SQL Server time type.</summary>
    public const string sql_time = "time";

    /// <summary>SQL Server timestamp type.</summary>
    public const string sql_timestamp = "timestamp";

    /// <summary>SQL Server rowversion type.</summary>
    public const string sql_rowversion = "rowversion";

    /// <summary>SQL Server uniqueidentifier type.</summary>
    public const string sql_uniqueidentifier = "uniqueidentifier";

    /// <summary>SQL Server char type.</summary>
    public const string sql_char = "char";

    /// <summary>SQL Server varchar type.</summary>
    public const string sql_varchar = "varchar";

    /// <summary>SQL Server text type.</summary>
    public const string sql_text = "text";

    /// <summary>SQL Server nchar type.</summary>
    public const string sql_nchar = "nchar";

    /// <summary>SQL Server nvarchar type.</summary>
    public const string sql_nvarchar = "nvarchar";

    /// <summary>SQL Server ntext type.</summary>
    public const string sql_ntext = "ntext";

    /// <summary>SQL Server binary type.</summary>
    public const string sql_binary = "binary";

    /// <summary>SQL Server varbinary type.</summary>
    public const string sql_varbinary = "varbinary";

    /// <summary>SQL Server image type.</summary>
    public const string sql_image = "image";

    /// <summary>SQL Server geometry type.</summary>
    public const string sql_geometry = "geometry";

    /// <summary>SQL Server geography type.</summary>
    public const string sql_geography = "geography";

    /// <summary>SQL Server hierarchyid type.</summary>
    public const string sql_hierarchyid = "hierarchyid";

    /// <summary>SQL Server sql_variant type.</summary>
    public const string sql_variant = "sql_variant";

    /// <summary>SQL Server xml type.</summary>
    public const string sql_xml = "xml";

    /// <summary>SQL Server cursor type.</summary>
    public const string sql_cursor = "cursor";

    /// <summary>SQL Server table type.</summary>
    public const string sql_table = "table";
}
