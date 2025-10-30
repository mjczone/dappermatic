// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System;
using System.Data;
using System.Globalization;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Smart type handler for NpgsqlLine with provider-specific optimization.
/// PostgreSQL: Native line type (infinite line represented by equation Ax + By + C = 0)
/// Others: Extended WKT format - "LINE(a b c)"
/// </summary>
public class SmartNpgsqlLineTypeHandler : SqlMapper.ITypeHandler
{
    /// <summary>
    /// Sets the parameter value for a line.
    /// PostgreSQL: Passes line directly (Npgsql converts NpgsqlLine to native PostgreSQL line).
    /// Other providers: Serializes line to extended WKT format.
    /// </summary>
    /// <param name="parameter">The database parameter to set.</param>
    /// <param name="value">The NpgsqlLine value to store.</param>
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
            // PostgreSQL: Native line type handling
            parameter.Value = value;
        }
        else
        {
            // Other providers: Extended WKT serialization
            var valueType = value.GetType();
            var a = valueType.GetProperty("A")?.GetValue(value);
            var b = valueType.GetProperty("B")?.GetValue(value);
            var c = valueType.GetProperty("C")?.GetValue(value);

            parameter.Value = $"LINE({a} {b} {c})";
            parameter.DbType = DbType.String;
        }
    }

    /// <summary>
    /// Parses a database value back to NpgsqlLine.
    /// PostgreSQL: Value is already NpgsqlLine from Npgsql.
    /// Other providers: Deserializes from extended WKT format.
    /// </summary>
    /// <param name="destinationType">The target type (NpgsqlLine).</param>
    /// <param name="value">The database value to parse.</param>
    /// <returns>An NpgsqlLine instance.</returns>
    public object? Parse(Type destinationType, object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        var valueType = value.GetType();

        // Check if value is already an NpgsqlLine (PostgreSQL native)
        if (valueType.Name == "NpgsqlLine")
        {
            return value;
        }

        // Parse from extended WKT format (other providers)
        var wkt = value.ToString() ?? string.Empty;

        // Expected format: "LINE(a b c)"
#pragma warning disable CA1865 // Use char overload (no StringComparison overload available for char)
        if (
            !wkt.StartsWith("LINE(", StringComparison.OrdinalIgnoreCase) || !wkt.EndsWith(")", StringComparison.Ordinal)
        )
#pragma warning restore CA1865
        {
            throw new FormatException($"Invalid WKT format for line. Expected 'LINE(a b c)', got: {wkt}");
        }

        var coeffs = wkt.Substring(5, wkt.Length - 6).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (coeffs.Length != 3)
        {
            throw new FormatException(
                $"Invalid WKT format for line. Expected 3 coefficients (a b c), got: {coeffs.Length}"
            );
        }

        var a = double.Parse(coeffs[0], CultureInfo.InvariantCulture);
        var b = double.Parse(coeffs[1], CultureInfo.InvariantCulture);
        var c = double.Parse(coeffs[2], CultureInfo.InvariantCulture);

        // Create NpgsqlLine using reflection
        var lineType = Type.GetType("NpgsqlTypes.NpgsqlLine, Npgsql");
        if (lineType == null)
        {
            throw new InvalidOperationException("NpgsqlLine type not found. Ensure Npgsql package is referenced.");
        }

        var ctor = lineType.GetConstructor(new[] { typeof(double), typeof(double), typeof(double) });
        if (ctor == null)
        {
            throw new InvalidOperationException("Could not find appropriate constructor for NpgsqlLine.");
        }

        return ctor.Invoke(new object[] { a, b, c });
    }
}
