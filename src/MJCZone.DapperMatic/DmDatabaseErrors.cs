// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data.Common;
using System.Globalization;
using System.Reflection;

namespace MJCZone.DapperMatic;

/// <summary>
/// What kind of error a database reported, the same on every provider.
/// </summary>
public enum DmDatabaseErrorKind
{
    /// <summary>Not a database error, or one not recognized.</summary>
    Unknown,

    /// <summary>A row with the same primary key or unique value exists.</summary>
    UniqueViolation,

    /// <summary>A row refers to a row that does not exist, or is referred to by rows that do.</summary>
    ForeignKeyViolation,

    /// <summary>A required value is missing.</summary>
    NotNullViolation,

    /// <summary>A value breaks a check constraint.</summary>
    CheckViolation,

    /// <summary>A value is too long, or too large, for its column.</summary>
    ValueOutOfRange,

    /// <summary>A value is not of its column's type.</summary>
    InvalidValue,

    /// <summary>The statement was chosen as a deadlock victim, or its serialization failed: running it again may work.</summary>
    Deadlock,

    /// <summary>The statement or a lock wait timed out.</summary>
    Timeout,

    /// <summary>The database refused the operation to the connection's user.</summary>
    PermissionDenied,

    /// <summary>A table, view, column or other object named in the statement does not exist.</summary>
    ObjectNotFound,

    /// <summary>The database could not be reached or refused the connection.</summary>
    ConnectionFailed,
}

/// <summary>
/// Classifies errors the database drivers throw, by their error codes rather than their messages, without referencing
/// any driver: SQL Server error numbers, PostgreSQL and MySQL SQLSTATEs and error numbers, SQLite result codes.
/// </summary>
public static class DmDatabaseErrors
{
    /// <summary>
    /// Classifies an error, or the first database error it wraps.
    /// </summary>
    /// <param name="exception">The error.</param>
    /// <returns>Its kind; <see cref="DmDatabaseErrorKind.Unknown"/> for an error that is not a database's or not recognized.</returns>
    public static DmDatabaseErrorKind Classify(Exception? exception)
    {
        for (var inner = exception; inner is not null; inner = inner.InnerException)
        {
            if (inner is TimeoutException)
            {
                return DmDatabaseErrorKind.Timeout;
            }

            if (inner is DbException error)
            {
                return ClassifyDbException(error);
            }
        }

        return DmDatabaseErrorKind.Unknown;
    }

    /// <summary>
    /// Tells whether an error, or the database error it wraps, is one that running the operation again may get past.
    /// </summary>
    /// <param name="exception">The error.</param>
    /// <returns>True for a deadlock, a timeout or a failed connection.</returns>
    public static bool IsTransient(Exception? exception) =>
        Classify(exception) is DmDatabaseErrorKind.Deadlock or DmDatabaseErrorKind.Timeout or DmDatabaseErrorKind.ConnectionFailed;

    private static DmDatabaseErrorKind ClassifyDbException(DbException error)
    {
        var type = error.GetType().FullName ?? string.Empty;
        if (type.StartsWith("Microsoft.Data.SqlClient", StringComparison.Ordinal) || type.StartsWith("System.Data.SqlClient", StringComparison.Ordinal))
        {
            return Number(error) switch
            {
                2627 or 2601 => DmDatabaseErrorKind.UniqueViolation,
                547 => error.Message.Contains("CHECK constraint", StringComparison.OrdinalIgnoreCase)
                    ? DmDatabaseErrorKind.CheckViolation
                    : DmDatabaseErrorKind.ForeignKeyViolation,
                515 => DmDatabaseErrorKind.NotNullViolation,
                8152 or 2628 or 8115 or 220 => DmDatabaseErrorKind.ValueOutOfRange,
                245 or 8114 or 241 or 242 or 295 or 8169 => DmDatabaseErrorKind.InvalidValue,
                1205 => DmDatabaseErrorKind.Deadlock,
                -2 or 1222 => DmDatabaseErrorKind.Timeout,
                229 or 230 or 262 or 300 => DmDatabaseErrorKind.PermissionDenied,
                208 or 207 or 2812 or 4902 => DmDatabaseErrorKind.ObjectNotFound,
                4060 or 18456 or 53 or 40 or 10054 or 10060 or 233 or 40613 => DmDatabaseErrorKind.ConnectionFailed,
                _ => DmDatabaseErrorKind.Unknown,
            };
        }

        if (type.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            // Extended result codes where the driver gives them (Microsoft.Data.Sqlite), else the primary code.
            var extended = Int(error, "SqliteExtendedErrorCode") ?? Int(error, "ErrorCode");
            var primary = Int(error, "SqliteErrorCode") ?? Int(error, "ResultCode") ?? extended & 0xFF;
            return extended switch
            {
                2067 or 1555 => DmDatabaseErrorKind.UniqueViolation,
                787 => DmDatabaseErrorKind.ForeignKeyViolation,
                1299 => DmDatabaseErrorKind.NotNullViolation,
                275 => DmDatabaseErrorKind.CheckViolation,
                _ => primary switch
                {
                    // SQLITE_CONSTRAINT without its extended code (System.Data.SQLite by default): the message says which.
                    19 => error.Message switch
                    {
                        var m when m.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase) => DmDatabaseErrorKind.UniqueViolation,
                        var m when m.Contains("FOREIGN KEY constraint", StringComparison.OrdinalIgnoreCase) => DmDatabaseErrorKind.ForeignKeyViolation,
                        var m when m.Contains("NOT NULL constraint", StringComparison.OrdinalIgnoreCase) => DmDatabaseErrorKind.NotNullViolation,
                        var m when m.Contains("CHECK constraint", StringComparison.OrdinalIgnoreCase) => DmDatabaseErrorKind.CheckViolation,
                        _ => DmDatabaseErrorKind.Unknown,
                    },
                    5 or 6 => DmDatabaseErrorKind.Timeout, // SQLITE_BUSY, SQLITE_LOCKED
                    3 => DmDatabaseErrorKind.PermissionDenied, // SQLITE_PERM
                    14 => DmDatabaseErrorKind.ConnectionFailed, // SQLITE_CANTOPEN
                    18 or 25 => DmDatabaseErrorKind.ValueOutOfRange, // SQLITE_TOOBIG, SQLITE_RANGE
                    20 => DmDatabaseErrorKind.InvalidValue, // SQLITE_MISMATCH
                    1 when error.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase)
                        || error.Message.Contains("no such column", StringComparison.OrdinalIgnoreCase) => DmDatabaseErrorKind.ObjectNotFound,
                    _ => DmDatabaseErrorKind.Unknown,
                },
            };
        }

        if (type.StartsWith("MySql", StringComparison.Ordinal))
        {
            // MySqlConnector's ErrorCode is an enum of the server's numbers; MySql.Data's Number is the number.
            var number = Int(error, "ErrorCode") is { } code && code > 0 && code < 100_000 ? code : Number(error);
            return number switch
            {
                1062 or 1586 => DmDatabaseErrorKind.UniqueViolation,
                1451 or 1452 or 1216 or 1217 => DmDatabaseErrorKind.ForeignKeyViolation,
                1048 or 1364 => DmDatabaseErrorKind.NotNullViolation,
                3819 or 4025 => DmDatabaseErrorKind.CheckViolation,
                1406 or 1264 or 1690 => DmDatabaseErrorKind.ValueOutOfRange,
                1366 or 1292 or 1265 or 1411 => DmDatabaseErrorKind.InvalidValue,
                1213 => DmDatabaseErrorKind.Deadlock,
                1205 or 3024 => DmDatabaseErrorKind.Timeout,
                1044 or 1045 or 1142 or 1143 or 1227 => DmDatabaseErrorKind.PermissionDenied,
                1146 or 1054 or 1049 or 1305 => DmDatabaseErrorKind.ObjectNotFound,
                1040 or 1042 or 1043 or 2002 or 2003 or 2006 or 2013 => DmDatabaseErrorKind.ConnectionFailed,
                _ => FromSqlState(error.SqlState),
            };
        }

        // PostgreSQL (Npgsql) and any driver that reports a standard SQLSTATE.
        return FromSqlState(error.SqlState);
    }

    private static DmDatabaseErrorKind FromSqlState(string? sqlState) =>
        sqlState switch
        {
            null => DmDatabaseErrorKind.Unknown,
            _ when sqlState.Length == 0 => DmDatabaseErrorKind.Unknown,
            "23505" => DmDatabaseErrorKind.UniqueViolation,
            "23503" => DmDatabaseErrorKind.ForeignKeyViolation,
            "23502" => DmDatabaseErrorKind.NotNullViolation,
            "23514" => DmDatabaseErrorKind.CheckViolation,
            "22001" or "22003" or "22008" => DmDatabaseErrorKind.ValueOutOfRange,
            "22P02" or "22007" or "22018" or "42804" => DmDatabaseErrorKind.InvalidValue,
            "40P01" or "40001" => DmDatabaseErrorKind.Deadlock,
            "57014" or "55P03" => DmDatabaseErrorKind.Timeout,
            "42501" => DmDatabaseErrorKind.PermissionDenied,
            "42P01" or "42703" or "42883" or "3F000" or "3D000" => DmDatabaseErrorKind.ObjectNotFound,
            _ when sqlState.StartsWith("08", StringComparison.Ordinal) || sqlState.StartsWith("28", StringComparison.Ordinal)
                || sqlState == "57P03" => DmDatabaseErrorKind.ConnectionFailed,
            _ => DmDatabaseErrorKind.Unknown,
        };

    private static int? Number(DbException error) => Int(error, "Number");

    // The most derived property of the name: MySqlConnector hides DbException's int ErrorCode with an enum, which a
    // search of the whole hierarchy would find twice.
    private static int? Int(DbException error, string property)
    {
        for (var type = error.GetType(); type is not null; type = type.BaseType)
        {
            if (type.GetProperty(property, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) is { } found)
            {
                return found.GetValue(error) switch
                {
                    int i => i,
                    Enum e => Convert.ToInt32(e, CultureInfo.InvariantCulture),
                    _ => null,
                };
            }
        }

        return null;
    }
}
