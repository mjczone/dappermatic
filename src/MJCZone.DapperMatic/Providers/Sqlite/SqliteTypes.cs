// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace MJCZone.DapperMatic.Providers.Sqlite;

/// <summary>
/// Provides constants for SQLite data types.
/// </summary>
/// <remarks>
/// See: https://www.sqlite.org/datatype3.html
/// ...
/// </remarks>
[SuppressMessage(
    "ReSharper",
    "InconsistentNaming",
    Justification = "Constants are named to match SQLite documentation."
)]
public static class SqliteTypes
{
    // integers

    /// <summary>INTEGER affinity type.</summary>
    public const string sql_integer = "integer";

    /// <summary>INT affinity type.</summary>
    public const string sql_int = "int";

    /// <summary>TINYINT affinity type.</summary>
    public const string sql_tinyint = "tinyint";

    /// <summary>SMALLINT affinity type.</summary>
    public const string sql_smallint = "smallint";

    /// <summary>MEDIUMINT affinity type.</summary>
    public const string sql_mediumint = "mediumint";

    /// <summary>BIGINT affinity type.</summary>
    public const string sql_bigint = "bigint";

    /// <summary>UNSIGNED BIG INT affinity type.</summary>
    public const string sql_unsigned_big_int = "unsigned big int";

    /// <summary>INT2 affinity type.</summary>
    public const string sql_int2 = "int2";

    /// <summary>INT4 affinity type.</summary>
    public const string sql_int4 = "int4";

    /// <summary>INT8 affinity type.</summary>
    public const string sql_int8 = "int8";

    // real

    /// <summary>REAL affinity type.</summary>
    public const string sql_real = "real";

    /// <summary>DOUBLE affinity type.</summary>
    public const string sql_double = "double";

    /// <summary>DOUBLE PRECISION affinity type.</summary>
    public const string sql_double_precision = "double precision";

    /// <summary>FLOAT affinity type.</summary>
    public const string sql_float = "float";

    /// <summary>NUMERIC affinity type.</summary>
    public const string sql_numeric = "numeric";

    /// <summary>DECIMAL affinity type.</summary>
    public const string sql_decimal = "decimal";

    // bool

    /// <summary>BOOL type stored as numeric.</summary>
    public const string sql_bool = "bool";

    /// <summary>BOOLEAN type stored as numeric.</summary>
    public const string sql_boolean = "boolean";

    // datetime

    /// <summary>DATE type stored as numeric.</summary>
    public const string sql_date = "date";

    /// <summary>DATETIME type stored as numeric.</summary>
    public const string sql_datetime = "datetime";

    /// <summary>TIMESTAMP type stored as numeric.</summary>
    public const string sql_timestamp = "timestamp";

    /// <summary>TIME type stored as numeric.</summary>
    public const string sql_time = "time";

    /// <summary>YEAR type stored as numeric.</summary>
    public const string sql_year = "year";

    // text

    /// <summary>CHAR affinity type.</summary>
    public const string sql_char = "char";

    /// <summary>NCHAR affinity type.</summary>
    public const string sql_nchar = "nchar";

    /// <summary>VARCHAR affinity type.</summary>
    public const string sql_varchar = "varchar";

    /// <summary>NVARCHAR affinity type.</summary>
    public const string sql_nvarchar = "nvarchar";

    /// <summary>CHARACTER affinity type.</summary>
    public const string sql_character = "character";

    /// <summary>VARYING CHARACTER affinity type.</summary>
    public const string sql_varying_character = "varying character";

    /// <summary>NATIVE CHARACTER affinity type.</summary>
    public const string sql_native_character = "native character";

    /// <summary>TEXT affinity type.</summary>
    public const string sql_text = "text";

    /// <summary>CLOB affinity type.</summary>
    public const string sql_clob = "clob";

    // binary

    /// <summary>BLOB affinity type.</summary>
    public const string sql_blob = "blob";
}
