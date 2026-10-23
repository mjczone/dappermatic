// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Globalization;
using Dapper;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// Converts a value the database returns to the type .NET reads it as, the way Dapper would for a column of that type.
/// </summary>
internal static class DmlValues
{
    /// <summary>
    /// Converts a value read from the database.
    /// </summary>
    /// <param name="value">The value, or null or <see cref="DBNull"/>.</param>
    /// <param name="type">The type to read it as.</param>
    /// <returns>The value, or null for NULL.</returns>
    /// <exception cref="InvalidOperationException">The value is NULL and the type cannot be null.</exception>
    public static object? Convert(object? value, Type type)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        if (value is null or DBNull)
        {
            return !type.IsValueType || target != type
                ? null
                : throw new InvalidOperationException(
                    $"The database returned NULL for a {type.Name}, which cannot be null: a related row is missing, or a "
                        + "value over no rows. Read it as a nullable, as (int?)o.Customer!.Id."
                );
        }

        if (target.IsInstanceOfType(value))
        {
            return value;
        }

#pragma warning disable CS0618 // "For internal use": it is how Dapper finds the handler that reads a type.
        SqlMapper.LookupDbType(target, "value", false, out var handler);
#pragma warning restore CS0618
        if (handler is not null)
        {
            return handler.Parse(target, value);
        }

        // Types Dapper maps itself, as a provider without them returns them: SQLite's text and integers, mostly.
        var invariant = CultureInfo.InvariantCulture;
        return (target, value) switch
        {
            _ when target.IsEnum => Enum.ToObject(target, value),
            (_, byte[] bytes) when target == typeof(Guid) => new Guid(bytes),
            (_, _) when target == typeof(Guid) => Guid.Parse(value.ToString()!),
            (_, string text) when target == typeof(bool) => text == "1" || bool.Parse(text),
            (_, DateTime date) when target == typeof(DateOnly) => DateOnly.FromDateTime(date),
            (_, string text) when target == typeof(DateOnly) => DateOnly.Parse(text, invariant),
            (_, TimeSpan time) when target == typeof(TimeOnly) => TimeOnly.FromTimeSpan(time),
            (_, DateTime time) when target == typeof(TimeOnly) => TimeOnly.FromDateTime(time),
            (_, string text) when target == typeof(TimeOnly) => TimeOnly.Parse(text, invariant),
            (_, string text) when target == typeof(TimeSpan) => TimeSpan.Parse(text, invariant),
            (_, DateTime date) when target == typeof(DateTimeOffset) => new DateTimeOffset(date),
            (_, string text) when target == typeof(DateTimeOffset) => DateTimeOffset.Parse(text, invariant),
            (_, string text) when target == typeof(DateTime) => DateTime.Parse(text, invariant),
            _ => System.Convert.ChangeType(value, target, invariant),
        };
    }
}
