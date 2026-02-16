// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Globalization;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Smart type handler for scalar DateOnly, TimeOnly, and DateTimeOffset values with runtime provider detection.
/// PostgreSQL: Uses native date/time types with Npgsql 9.x compatibility.
/// Other providers: Uses native support where available, string conversion for SQLite.
/// </summary>
/// <typeparam name="T">The value type (DateOnly, TimeOnly, or DateTimeOffset).</typeparam>
public class SmartScalarDateTimeTypeHandler<T> : SqlMapper.TypeHandler<T?>
    where T : struct
{
    /// <summary>
    /// Sets the parameter value for a scalar temporal value.
    /// PostgreSQL: Passes value directly (Npgsql handles conversion).
    /// Other providers: Passes value directly (native support).
    /// SQLite: May require string conversion.
    /// </summary>
    /// <param name="parameter">The database parameter to set.</param>
    /// <param name="value">The temporal value to store.</param>
    public override void SetValue(IDbDataParameter parameter, T? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
            // Still set DbType so Npgsql knows the column type for NULL params
            SetDbType(parameter);
            return;
        }

        // Runtime provider detection via parameter type
        var paramType = parameter.GetType().FullName ?? string.Empty;

        if (paramType.Contains("Npgsql", StringComparison.Ordinal))
        {
            // PostgreSQL: Set DbType explicitly so Npgsql maps correctly
            SetDbType(parameter);
            parameter.Value = value;
        }
        else if (paramType.Contains("SqlClient", StringComparison.Ordinal))
        {
            // SQL Server: SqlClient doesn't natively support DateOnly/TimeOnly,
            // convert to types it understands
            if (value.Value is DateOnly dateOnly)
            {
                parameter.DbType = DbType.Date;
                parameter.Value = dateOnly.ToDateTime(TimeOnly.MinValue);
            }
            else if (value.Value is TimeOnly timeOnly)
            {
                parameter.Value = timeOnly.ToTimeSpan();
            }
            else
            {
                parameter.Value = value;
            }
        }
        else if (paramType.Contains("MySql", StringComparison.Ordinal))
        {
            // MySQL: Set DbType and pass value directly
            SetDbType(parameter);
            parameter.Value = value;
        }
        else if (paramType.Contains("SQLite", StringComparison.Ordinal))
        {
            // SQLite: Convert to string format
            parameter.Value = FormatForSqlite(value.Value);
        }
        else
        {
            // Default: Set DbType and pass value directly
            SetDbType(parameter);
            parameter.Value = value;
        }
    }

    /// <summary>
    /// Parses the database value to a temporal type.
    /// Handles Npgsql 9.x compatibility conversions (DateTime ↔ DateOnly, TimeSpan ↔ TimeOnly).
    /// </summary>
    /// <param name="value">The database value to parse.</param>
    /// <returns>The parsed temporal value, or null if the value is null or DBNull.</returns>
    public override T? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        // If already the correct type
        if (value is T typedValue)
        {
            return typedValue;
        }

        // Npgsql 9.x compatibility: DateTime → DateOnly
        if (value is DateTime dt && typeof(T) == typeof(DateOnly))
        {
            return (T)(object)DateOnly.FromDateTime(dt);
        }

        // Npgsql 9.x compatibility: TimeSpan → TimeOnly
        if (value is TimeSpan ts && typeof(T) == typeof(TimeOnly))
        {
            return (T)(object)TimeOnly.FromTimeSpan(ts);
        }

        // SQLite compatibility: DateTime → TimeOnly (SQLite returns DateTime for time columns)
        if (value is DateTime dtTime && typeof(T) == typeof(TimeOnly))
        {
            return (T)(object)TimeOnly.FromDateTime(dtTime);
        }

        // Npgsql 9.x compatibility: DateTime → DateTimeOffset
        if (value is DateTime dt2 && typeof(T) == typeof(DateTimeOffset))
        {
            return (T)(object)new DateTimeOffset(SpecifyKindAsUtc(dt2));
        }

        // Parse from string (SQLite and other string-based storage)
        if (value is string str)
        {
            return ParseFromString(str);
        }

        throw new InvalidOperationException($"Cannot convert {value.GetType().Name} to {typeof(T).Name}");
    }

    /// <summary>
    /// Sets DbType explicitly so Npgsql maps the parameter to the correct PostgreSQL type.
    /// Without this, Npgsql may infer text instead of date/time when Dapper unwraps nullable values.
    /// </summary>
    private static void SetDbType(IDbDataParameter parameter)
    {
        if (typeof(T) == typeof(DateOnly))
        {
            parameter.DbType = DbType.Date;
        }
        else if (typeof(T) == typeof(TimeOnly))
        {
            parameter.DbType = DbType.Time;
        }
        else if (typeof(T) == typeof(DateTimeOffset))
        {
            parameter.DbType = DbType.DateTimeOffset;
        }
    }

    /// <summary>
    /// Formats a temporal value for SQLite storage (string-based).
    /// </summary>
    private static string FormatForSqlite(T value)
    {
        if (value is DateOnly dateOnly)
        {
            return dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (value is TimeOnly timeOnly)
        {
            return timeOnly.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture);
        }

        if (value is DateTimeOffset dto)
        {
            return dto.ToString("yyyy-MM-dd HH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture);
        }

        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Parses a temporal value from a string (SQLite and string-based storage).
    /// </summary>
    private static T? ParseFromString(string str)
    {
        if (string.IsNullOrWhiteSpace(str))
        {
            return null;
        }

        if (typeof(T) == typeof(DateOnly))
        {
            if (DateOnly.TryParse(str, CultureInfo.InvariantCulture, out var dateOnly))
            {
                return (T)(object)dateOnly;
            }
        }

        if (typeof(T) == typeof(TimeOnly))
        {
            if (TimeOnly.TryParse(str, CultureInfo.InvariantCulture, out var timeOnly))
            {
                return (T)(object)timeOnly;
            }
        }

        if (typeof(T) == typeof(DateTimeOffset))
        {
            if (DateTimeOffset.TryParse(str, CultureInfo.InvariantCulture, out var dto))
            {
                return (T)(object)dto;
            }
        }

        throw new FormatException($"Cannot parse '{str}' as {typeof(T).Name}");
    }

    /// <summary>
    /// Ensures a DateTime is treated as UTC for PostgreSQL compatibility.
    /// PostgreSQL timestamptz stores UTC, so unspecified DateTimes are treated as UTC.
    /// </summary>
    private static DateTime SpecifyKindAsUtc(DateTime dt)
    {
        return dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt;
    }
}
