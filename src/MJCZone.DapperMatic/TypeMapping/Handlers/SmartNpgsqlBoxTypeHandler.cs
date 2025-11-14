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
/// Smart type handler for NpgsqlBox with provider-specific optimization.
/// PostgreSQL: Native box type
/// Others: WKT (Well-Known Text) format - "POLYGON((x1 y1, x2 y1, x2 y2, x1 y2, x1 y1))"
/// </summary>
public class SmartNpgsqlBoxTypeHandler : SqlMapper.ITypeHandler
{
    private static readonly string[] PointSeparator = new[] { "),(" };

    /// <summary>
    /// Sets the parameter value for a box.
    /// PostgreSQL: Passes box directly (Npgsql converts NpgsqlBox to native PostgreSQL box).
    /// Other providers: Serializes box to WKT polygon format.
    /// </summary>
    /// <param name="parameter">The database parameter to set.</param>
    /// <param name="value">The NpgsqlBox value to store.</param>
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
            // PostgreSQL: Native box type handling
            parameter.Value = value;
        }
        else
        {
            // Other providers: WKT serialization as polygon
            var valueType = value.GetType();
            var upperRight = valueType.GetProperty("UpperRight")?.GetValue(value);
            var lowerLeft = valueType.GetProperty("LowerLeft")?.GetValue(value);

            if (upperRight == null || lowerLeft == null)
            {
                parameter.Value = DBNull.Value;
                return;
            }

            var urType = upperRight.GetType();
            var llType = lowerLeft.GetType();

            var x1 = llType.GetProperty("X")?.GetValue(lowerLeft);
            var y1 = llType.GetProperty("Y")?.GetValue(lowerLeft);
            var x2 = urType.GetProperty("X")?.GetValue(upperRight);
            var y2 = urType.GetProperty("Y")?.GetValue(upperRight);

            // Box as closed polygon (5 points, first == last)
            parameter.Value = $"POLYGON(({x1} {y1},{x2} {y1},{x2} {y2},{x1} {y2},{x1} {y1}))";
            parameter.DbType = DbType.String;
        }
    }

    /// <summary>
    /// Parses a database value back to NpgsqlBox.
    /// PostgreSQL: Value is already NpgsqlBox from Npgsql.
    /// Other providers: Deserializes from WKT polygon format.
    /// </summary>
    /// <param name="destinationType">The target type (NpgsqlBox).</param>
    /// <param name="value">The database value to parse.</param>
    /// <returns>An NpgsqlBox instance.</returns>
    public object? Parse(Type destinationType, object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        var valueType = value.GetType();

        // Check if value is already an NpgsqlBox (PostgreSQL native)
        if (valueType.Name == "NpgsqlBox")
        {
            return value;
        }

        // Parse from string format (other providers)
        var str = value.ToString() ?? string.Empty;

        double x1,
            y1,
            x2,
            y2;

        // Try PostgreSQL native format: "(x1,y1),(x2,y2)"
        if (str.Contains("),(", StringComparison.Ordinal))
        {
            var parts = str.Split(PointSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                throw new FormatException($"Invalid PostgreSQL box format. Expected '(x1,y1),(x2,y2)', got: {str}");
            }

            // Remove leading/trailing parentheses
            var p1Str = parts[0].TrimStart('(');
            var p2Str = parts[1].TrimEnd(')');

            var p1Coords = p1Str.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var p2Coords = p2Str.Split(',', StringSplitOptions.RemoveEmptyEntries);

            if (p1Coords.Length != 2 || p2Coords.Length != 2)
            {
                throw new FormatException($"Invalid PostgreSQL box format. Expected '(x1,y1),(x2,y2)', got: {str}");
            }

            x1 = double.Parse(p1Coords[0], CultureInfo.InvariantCulture);
            y1 = double.Parse(p1Coords[1], CultureInfo.InvariantCulture);
            x2 = double.Parse(p2Coords[0], CultureInfo.InvariantCulture);
            y2 = double.Parse(p2Coords[1], CultureInfo.InvariantCulture);
        }
        // Try WKT polygon format: "POLYGON((x1 y1,x2 y1,x2 y2,x1 y2,x1 y1))"
        else if (str.StartsWith("POLYGON((", StringComparison.OrdinalIgnoreCase))
        {
            var coordsStr = str.Substring(9, str.Length - 11); // Remove "POLYGON((" and "))"
            var pointStrs = coordsStr.Split(',', StringSplitOptions.RemoveEmptyEntries);

            if (pointStrs.Length < 4)
            {
                throw new FormatException(
                    $"Invalid WKT polygon format for box. Expected at least 4 points, got: {str}"
                );
            }

            // Extract first point (lower-left) and third point (upper-right)
            var p1Coords = pointStrs[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var p3Coords = pointStrs[2].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            x1 = double.Parse(p1Coords[0], CultureInfo.InvariantCulture);
            y1 = double.Parse(p1Coords[1], CultureInfo.InvariantCulture);
            x2 = double.Parse(p3Coords[0], CultureInfo.InvariantCulture);
            y2 = double.Parse(p3Coords[1], CultureInfo.InvariantCulture);
        }
        else
        {
            throw new FormatException($"Invalid box format. Expected '(x1,y1),(x2,y2)' or 'POLYGON((...))' got: {str}");
        }

        // Create NpgsqlPoint instances for corners
        var pointType = Type.GetType("NpgsqlTypes.NpgsqlPoint, Npgsql");
        if (pointType == null)
        {
            throw new InvalidOperationException("NpgsqlPoint type not found. Ensure Npgsql package is referenced.");
        }

        var pointCtor = pointType.GetConstructor(new[] { typeof(double), typeof(double) });
        if (pointCtor == null)
        {
            throw new InvalidOperationException("Could not find appropriate constructor for NpgsqlPoint.");
        }

        var lowerLeft = pointCtor.Invoke(new object[] { x1, y1 });
        var upperRight = pointCtor.Invoke(new object[] { x2, y2 });

        // Create NpgsqlBox using reflection
        var boxType = Type.GetType("NpgsqlTypes.NpgsqlBox, Npgsql");
        if (boxType == null)
        {
            throw new InvalidOperationException("NpgsqlBox type not found. Ensure Npgsql package is referenced.");
        }

        var boxCtor = boxType.GetConstructor(new[] { pointType, pointType });
        if (boxCtor == null)
        {
            throw new InvalidOperationException("Could not find appropriate constructor for NpgsqlBox.");
        }

        return boxCtor.Invoke(new[] { upperRight, lowerLeft });
    }
}
