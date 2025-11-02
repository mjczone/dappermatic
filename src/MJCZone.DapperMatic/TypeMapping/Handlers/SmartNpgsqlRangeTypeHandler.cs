// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System;
using System.Data;
using System.Text.Json;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Smart type handler for NpgsqlRange&lt;T&gt; with provider-specific optimization.
/// PostgreSQL: Native range types (int4range, int8range, numrange, daterange, tsrange, tstzrange)
/// Others: JSON serialization with bounds and inclusivity metadata
/// </summary>
/// <typeparam name="T">The type parameter for the range (int, long, decimal, DateTime, DateOnly, DateTimeOffset)</typeparam>
public class SmartNpgsqlRangeTypeHandler<T> : SqlMapper.ITypeHandler
    where T : struct
{
    /// <summary>
    /// Sets the parameter value for a range.
    /// PostgreSQL: Passes range directly (Npgsql converts NpgsqlRange&lt;T&gt; to native PostgreSQL range).
    /// Other providers: Serializes range to JSON string.
    /// </summary>
    /// <param name="parameter">The database parameter to set.</param>
    /// <param name="value">The NpgsqlRange&lt;T&gt; value to store.</param>
    public void SetValue(IDbDataParameter parameter, object? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
            return;
        }

        var paramType = parameter.GetType().FullName ?? string.Empty;

        if (paramType.Contains("Npgsql", StringComparison.Ordinal))
        {
            // PostgreSQL: Native range type handling
            // Npgsql automatically handles NpgsqlRange<T> → SQL range conversion
            parameter.Value = value;
        }
        else
        {
            // Other providers: JSON serialization
            // Extract range properties using reflection
            var valueType = value.GetType();
            var lowerBound = valueType.GetProperty("LowerBound")?.GetValue(value);
            var upperBound = valueType.GetProperty("UpperBound")?.GetValue(value);
            var lowerBoundInclusive = valueType.GetProperty("LowerBoundIsInclusive")?.GetValue(value) ?? false;
            var upperBoundInclusive = valueType.GetProperty("UpperBoundIsInclusive")?.GetValue(value) ?? false;
            var lowerBoundInfinite = valueType.GetProperty("LowerBoundInfinite")?.GetValue(value) ?? false;
            var upperBoundInfinite = valueType.GetProperty("UpperBoundInfinite")?.GetValue(value) ?? false;

            var rangeObj = new
            {
                LowerBound = lowerBound,
                UpperBound = upperBound,
                LowerBoundIsInclusive = lowerBoundInclusive,
                UpperBoundIsInclusive = upperBoundInclusive,
                LowerBoundInfinite = lowerBoundInfinite,
                UpperBoundInfinite = upperBoundInfinite,
            };

            parameter.Value = JsonSerializer.Serialize(rangeObj);
            parameter.DbType = DbType.String;
        }
    }

    /// <summary>
    /// Parses a database value back to NpgsqlRange&lt;T&gt;.
    /// PostgreSQL: Value is already NpgsqlRange&lt;T&gt; from Npgsql, or PostgreSQL native format string.
    /// Other providers: Deserializes from JSON string.
    /// </summary>
    /// <param name="destinationType">The target type (NpgsqlRange&lt;T&gt;).</param>
    /// <param name="value">The database value to parse.</param>
    /// <returns>A NpgsqlRange&lt;T&gt; instance.</returns>
    public object? Parse(Type destinationType, object value)
    {
        if (value == null || value is DBNull)
        {
            return GetDefaultRange();
        }

        // Check if value is already a NpgsqlRange<T> (PostgreSQL native)
        var valueType = value.GetType();
        if (valueType.IsGenericType && valueType.Name.StartsWith("NpgsqlRange", StringComparison.Ordinal))
        {
            // Get the generic argument of the incoming range
            var valueGenericArgs = valueType.GetGenericArguments();
            if (valueGenericArgs.Length == 1)
            {
                var valueInnerType = valueGenericArgs[0];

                // If types match, return as-is (existing behavior)
                if (valueInnerType == typeof(T))
                {
                    return value;
                }

                // Handle DateTime <-> DateTimeOffset conversion for Npgsql 9.x compatibility
                // Npgsql 9.x returns NpgsqlRange<DateTime> for both tsrange and tstzrange
                if ((valueInnerType == typeof(DateTime) && typeof(T) == typeof(DateTimeOffset)) ||
                    (valueInnerType == typeof(DateTimeOffset) && typeof(T) == typeof(DateTime)))
                {
                    return ConvertRangeType(value, valueType);
                }
            }

            // Type mismatch we can't handle - return as-is and let Dapper deal with it
            return value;
        }

        // Get the NpgsqlRange<T> type for reflection operations
        var rangeType = Type.GetType($"NpgsqlTypes.NpgsqlRange`1[[{typeof(T).AssemblyQualifiedName}]], Npgsql");
        if (rangeType == null)
        {
            throw new InvalidOperationException(
                $"NpgsqlRange<{typeof(T).Name}> type not found. Ensure Npgsql package is referenced."
            );
        }

        // Try PostgreSQL native format first: "[1,10]", "(1,10)", "[1,10)", "empty", etc.
        var str = value.ToString() ?? string.Empty;

        if (str.StartsWith('[') || str.StartsWith('(') || str.Equals("empty", StringComparison.OrdinalIgnoreCase))
        {
            // Call static NpgsqlRange<T>.Parse(string) method via reflection
            var parseMethod = rangeType.GetMethod("Parse", new[] { typeof(string) });
            if (parseMethod == null)
            {
                throw new InvalidOperationException(
                    $"Could not find Parse method on NpgsqlRange<{typeof(T).Name}>."
                );
            }

            return parseMethod.Invoke(null, new object[] { str });
        }

        // Deserialize from JSON (other providers or fallback)
        var json = str;
        using var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;

        var lowerBoundInfinite = root.TryGetProperty("LowerBoundInfinite", out var lbi) && lbi.GetBoolean();
        var upperBoundInfinite = root.TryGetProperty("UpperBoundInfinite", out var ubi) && ubi.GetBoolean();

        var lowerBound = lowerBoundInfinite ? default(T) : root.GetProperty("LowerBound").Deserialize<T>();
        var upperBound = upperBoundInfinite ? default(T) : root.GetProperty("UpperBound").Deserialize<T>();
        var lowerInclusive = root.TryGetProperty("LowerBoundIsInclusive", out var li) && li.GetBoolean();
        var upperInclusive = root.TryGetProperty("UpperBoundIsInclusive", out var ui) && ui.GetBoolean();

        // Find the constructor: NpgsqlRange(T lowerBound, bool lowerBoundIsInclusive, bool lowerBoundInfinite, T upperBound, bool upperBoundIsInclusive, bool upperBoundInfinite)
        var ctor = rangeType.GetConstructor(
            new[] { typeof(T), typeof(bool), typeof(bool), typeof(T), typeof(bool), typeof(bool) }
        );

        if (ctor == null)
        {
            throw new InvalidOperationException(
                $"Could not find appropriate constructor for NpgsqlRange<{typeof(T).Name}>."
            );
        }

        return ctor.Invoke(
            new object[]
            {
                lowerBound!,
                lowerInclusive,
                lowerBoundInfinite,
                upperBound!,
                upperInclusive,
                upperBoundInfinite,
            }
        );
    }

    /// <summary>
    /// Gets a default empty range for the type T.
    /// </summary>
    private static object GetDefaultRange()
    {
        var rangeType = Type.GetType($"NpgsqlTypes.NpgsqlRange`1[[{typeof(T).AssemblyQualifiedName}]], Npgsql");
        if (rangeType == null)
        {
            throw new InvalidOperationException(
                $"NpgsqlRange<{typeof(T).Name}> type not found. Ensure Npgsql package is referenced."
            );
        }

        // Create an empty range using the default constructor
        var defaultCtor = rangeType.GetConstructor(Type.EmptyTypes);
        if (defaultCtor != null)
        {
            return defaultCtor.Invoke(null);
        }

        // Fallback: Create using the parameterized constructor with all false/default values
        var ctor = rangeType.GetConstructor(
            new[] { typeof(T), typeof(bool), typeof(bool), typeof(T), typeof(bool), typeof(bool) }
        );

        if (ctor == null)
        {
            throw new InvalidOperationException(
                $"Could not find appropriate constructor for NpgsqlRange<{typeof(T).Name}>."
            );
        }

        return ctor.Invoke(new object[] { default(T)!, false, true, default(T)!, false, true });
    }

    /// <summary>
    /// Converts a NpgsqlRange from one type to another (e.g., DateTime to DateTimeOffset).
    /// This handles Npgsql 9.x compatibility where tstzrange returns NpgsqlRange&lt;DateTime&gt;
    /// but we expect NpgsqlRange&lt;DateTimeOffset&gt;.
    /// </summary>
    /// <param name="value">The incoming range value to convert.</param>
    /// <param name="valueType">The type of the incoming range.</param>
    /// <returns>A new NpgsqlRange&lt;T&gt; with converted bounds.</returns>
    private static object ConvertRangeType(object value, Type valueType)
    {
        // Extract bounds and metadata from the incoming range using reflection
        var lowerBound = valueType.GetProperty("LowerBound")?.GetValue(value);
        var upperBound = valueType.GetProperty("UpperBound")?.GetValue(value);
        var lowerBoundInclusive = (bool)(valueType.GetProperty("LowerBoundIsInclusive")?.GetValue(value) ?? false);
        var upperBoundInclusive = (bool)(valueType.GetProperty("UpperBoundIsInclusive")?.GetValue(value) ?? false);
        var lowerBoundInfinite = (bool)(valueType.GetProperty("LowerBoundInfinite")?.GetValue(value) ?? false);
        var upperBoundInfinite = (bool)(valueType.GetProperty("UpperBoundInfinite")?.GetValue(value) ?? false);

        // Convert bounds to target type T
        T convertedLowerBound = default!;
        T convertedUpperBound = default!;

        if (!lowerBoundInfinite && lowerBound != null)
        {
            convertedLowerBound = ConvertDateTimeValue<T>(lowerBound);
        }

        if (!upperBoundInfinite && upperBound != null)
        {
            convertedUpperBound = ConvertDateTimeValue<T>(upperBound);
        }

        // Reconstruct range with converted bounds
        var rangeType = Type.GetType($"NpgsqlTypes.NpgsqlRange`1[[{typeof(T).AssemblyQualifiedName}]], Npgsql");
        if (rangeType == null)
        {
            throw new InvalidOperationException(
                $"NpgsqlRange<{typeof(T).Name}> type not found. Ensure Npgsql package is referenced."
            );
        }

        var ctor = rangeType.GetConstructor(
            new[] { typeof(T), typeof(bool), typeof(bool), typeof(T), typeof(bool), typeof(bool) }
        );

        if (ctor == null)
        {
            throw new InvalidOperationException(
                $"Could not find appropriate constructor for NpgsqlRange<{typeof(T).Name}>."
            );
        }

        return ctor.Invoke(
            new object[]
            {
                convertedLowerBound!,
                lowerBoundInclusive,
                lowerBoundInfinite,
                convertedUpperBound!,
                upperBoundInclusive,
                upperBoundInfinite,
            }
        );
    }

    /// <summary>
    /// Converts between DateTime and DateTimeOffset types.
    /// Used for handling Npgsql 9.x compatibility where tstzrange returns DateTime
    /// but we expect DateTimeOffset.
    /// </summary>
    /// <typeparam name="TTarget">The target type (DateTime or DateTimeOffset).</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <returns>The converted value.</returns>
    private static TTarget ConvertDateTimeValue<TTarget>(object value)
    {
        if (value is DateTime dt && typeof(TTarget) == typeof(DateTimeOffset))
        {
            // Convert DateTime to DateTimeOffset
            // PostgreSQL timestamptz stores UTC, so treat unspecified as UTC
            var utcDateTime = dt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                : dt;
            return (TTarget)(object)new DateTimeOffset(utcDateTime);
        }

        if (value is DateTimeOffset dto && typeof(TTarget) == typeof(DateTime))
        {
            // Convert DateTimeOffset to DateTime
            return (TTarget)(object)dto.DateTime;
        }

        throw new InvalidOperationException(
            $"Cannot convert {value.GetType().Name} to {typeof(TTarget).Name}"
        );
    }
}
