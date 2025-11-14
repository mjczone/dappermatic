// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Text.Json;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Smart type handler for T[] arrays with runtime provider detection.
/// PostgreSQL: Uses native array types (text[], int4[], etc.) for 10-50x performance boost.
/// Other providers: Uses JSON array serialization for cross-database compatibility.
/// </summary>
/// <typeparam name="T">The array element type.</typeparam>
public class SmartArrayTypeHandler<T> : SqlMapper.TypeHandler<T[]>
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    /// <summary>
    /// Sets the parameter value for an array.
    /// PostgreSQL: Passes array directly (Npgsql converts T[] to native PostgreSQL array).
    /// Other providers: Serializes array to JSON string.
    /// </summary>
    /// <param name="parameter">The database parameter to set.</param>
    /// <param name="value">The array value to store.</param>
    public override void SetValue(IDbDataParameter parameter, T[]? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
            return;
        }

        // Runtime provider detection via parameter type
        var paramType = parameter.GetType().FullName ?? string.Empty;

        if (paramType.Contains("Npgsql", StringComparison.Ordinal))
        {
            // PostgreSQL: Use native array types (FASTEST - 10-50x faster than JSON)
            // Npgsql automatically converts:
            //   string[] → text[]
            //   int[] → int4[]
            //   long[] → int8[]
            //   etc.
            parameter.Value = value;
        }
        else
        {
            // Other providers (SQL Server, MySQL, SQLite): JSON array fallback
            // Works reliably across all databases
            var jsonString = JsonSerializer.Serialize(value, SerializerOptions);
            parameter.Value = jsonString;
        }
    }

    /// <summary>
    /// Parses the database value to an array.
    /// PostgreSQL: Returns native array directly (T[]).
    /// Other providers: Deserializes from JSON string.
    /// </summary>
    /// <param name="value">The database value to parse.</param>
    /// <returns>The parsed array, or null if the value is null or DBNull.</returns>
    public override T[]? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        // If already a typed array (PostgreSQL native array)
        if (value is T[] array)
        {
            return array;
        }

        // Handle DateTime[] ↔ DateTimeOffset[] conversion for Npgsql 9.x compatibility
        // Npgsql 9.x returns DateTime[] for timestamptz[] but we expect DateTimeOffset[]
        if (value is DateTime[] dateTimeArray && typeof(T) == typeof(DateTimeOffset))
        {
            return (T[])(object)ConvertDateTimeArray<DateTime, DateTimeOffset>(dateTimeArray);
        }

        if (value is DateTimeOffset[] dateTimeOffsetArray && typeof(T) == typeof(DateTime))
        {
            return (T[])(object)ConvertDateTimeArray<DateTimeOffset, DateTime>(dateTimeOffsetArray);
        }

        // Handle DateTime[] ↔ DateOnly[] conversion for Npgsql 9.x compatibility
        // Npgsql 9.x returns DateTime[] for date[] but we expect DateOnly[]
        if (value is DateTime[] dateTimeArray2 && typeof(T) == typeof(DateOnly))
        {
            return (T[])(object)ConvertDateTimeArray<DateTime, DateOnly>(dateTimeArray2);
        }

        if (value is DateOnly[] dateOnlyArray && typeof(T) == typeof(DateTime))
        {
            return (T[])(object)ConvertDateTimeArray<DateOnly, DateTime>(dateOnlyArray);
        }

        // Handle TimeSpan[] ↔ TimeOnly[] conversion for Npgsql 9.x compatibility
        // Npgsql 9.x returns TimeSpan[] for time[] but we expect TimeOnly[]
        if (value is TimeSpan[] timeSpanArray && typeof(T) == typeof(TimeOnly))
        {
            return (T[])(object)ConvertTimeArray<TimeSpan, TimeOnly>(timeSpanArray);
        }

        if (value is TimeOnly[] timeOnlyArray && typeof(T) == typeof(TimeSpan))
        {
            return (T[])(object)ConvertTimeArray<TimeOnly, TimeSpan>(timeOnlyArray);
        }

        // Deserialize from JSON (other providers)
        var jsonString = value.ToString();
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return null;
        }

        return JsonSerializer.Deserialize<T[]>(jsonString);
    }

    /// <summary>
    /// Converts array elements between DateTime, DateTimeOffset, and DateOnly types.
    /// Used for handling Npgsql 9.x compatibility where arrays return DateTime[]
    /// but we expect DateTimeOffset[] or DateOnly[].
    /// </summary>
    /// <typeparam name="TSource">The source array element type.</typeparam>
    /// <typeparam name="TTarget">The target array element type.</typeparam>
    /// <param name="sourceArray">The source array to convert.</param>
    /// <returns>The converted array with target element type.</returns>
    private static TTarget[] ConvertDateTimeArray<TSource, TTarget>(TSource[] sourceArray)
    {
        var result = new TTarget[sourceArray.Length];

        for (int i = 0; i < sourceArray.Length; i++)
        {
            var sourceValue = sourceArray[i];

            // DateTime → DateTimeOffset
            if (sourceValue is DateTime dt && typeof(TTarget) == typeof(DateTimeOffset))
            {
                // PostgreSQL timestamptz stores UTC, treat unspecified as UTC
                var utcDateTime = dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt;
                result[i] = (TTarget)(object)new DateTimeOffset(utcDateTime);
            }
            // DateTimeOffset → DateTime
            else if (sourceValue is DateTimeOffset dto && typeof(TTarget) == typeof(DateTime))
            {
                result[i] = (TTarget)(object)dto.DateTime;
            }
            // DateTime → DateOnly
            else if (sourceValue is DateTime dt2 && typeof(TTarget) == typeof(DateOnly))
            {
                // PostgreSQL date type only stores the date part, extract it
                result[i] = (TTarget)(object)DateOnly.FromDateTime(dt2);
            }
            // DateOnly → DateTime
            else if (sourceValue is DateOnly dateOnly && typeof(TTarget) == typeof(DateTime))
            {
                // Convert DateOnly to DateTime (use midnight for time component)
                result[i] = (TTarget)(object)dateOnly.ToDateTime(TimeOnly.MinValue);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Cannot convert {typeof(TSource).Name}[] to {typeof(TTarget).Name}[]"
                );
            }
        }

        return result;
    }

    /// <summary>
    /// Converts array elements between TimeSpan and TimeOnly types.
    /// Used for handling Npgsql 9.x compatibility where time[] returns TimeSpan[]
    /// but we expect TimeOnly[].
    /// </summary>
    /// <typeparam name="TSource">The source array element type.</typeparam>
    /// <typeparam name="TTarget">The target array element type.</typeparam>
    /// <param name="sourceArray">The source array to convert.</param>
    /// <returns>The converted array with target element type.</returns>
    private static TTarget[] ConvertTimeArray<TSource, TTarget>(TSource[] sourceArray)
    {
        var result = new TTarget[sourceArray.Length];

        for (int i = 0; i < sourceArray.Length; i++)
        {
            var sourceValue = sourceArray[i];

            // TimeSpan → TimeOnly
            if (sourceValue is TimeSpan ts && typeof(TTarget) == typeof(TimeOnly))
            {
                // PostgreSQL time type stores time of day, convert from TimeSpan
                result[i] = (TTarget)(object)TimeOnly.FromTimeSpan(ts);
            }
            // TimeOnly → TimeSpan
            else if (sourceValue is TimeOnly to && typeof(TTarget) == typeof(TimeSpan))
            {
                // Convert TimeOnly to TimeSpan
                result[i] = (TTarget)(object)to.ToTimeSpan();
            }
            else
            {
                throw new InvalidOperationException(
                    $"Cannot convert {typeof(TSource).Name}[] to {typeof(TTarget).Name}[]"
                );
            }
        }

        return result;
    }
}
