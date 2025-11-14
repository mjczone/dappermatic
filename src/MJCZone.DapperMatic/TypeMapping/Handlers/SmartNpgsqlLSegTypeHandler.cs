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
/// Smart type handler for NpgsqlLSeg (line segment) with provider-specific optimization.
/// PostgreSQL: Native lseg type
/// Others: WKT (Well-Known Text) format - "LINESTRING(x1 y1, x2 y2)"
/// </summary>
public class SmartNpgsqlLSegTypeHandler : SqlMapper.ITypeHandler
{
    private static readonly string[] PointSeparator = new[] { "),(" };

    /// <summary>
    /// Sets the parameter value for a line segment.
    /// PostgreSQL: Passes lseg directly (Npgsql converts NpgsqlLSeg to native PostgreSQL lseg).
    /// Other providers: Serializes lseg to WKT LINESTRING format.
    /// </summary>
    /// <param name="parameter">The database parameter to set.</param>
    /// <param name="value">The NpgsqlLSeg value to store.</param>
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
            // PostgreSQL: Native lseg type handling
            parameter.Value = value;
        }
        else
        {
            // Other providers: WKT LINESTRING serialization
            var valueType = value.GetType();
            var start = valueType.GetProperty("Start")?.GetValue(value);
            var end = valueType.GetProperty("End")?.GetValue(value);

            if (start == null || end == null)
            {
                parameter.Value = DBNull.Value;
                return;
            }

            var startType = start.GetType();
            var endType = end.GetType();

            var x1 = startType.GetProperty("X")?.GetValue(start);
            var y1 = startType.GetProperty("Y")?.GetValue(start);
            var x2 = endType.GetProperty("X")?.GetValue(end);
            var y2 = endType.GetProperty("Y")?.GetValue(end);

            parameter.Value = $"LINESTRING({x1} {y1}, {x2} {y2})";
            parameter.DbType = DbType.String;
        }
    }

    /// <summary>
    /// Parses a database value back to NpgsqlLSeg.
    /// PostgreSQL: Value is already NpgsqlLSeg from Npgsql.
    /// Other providers: Deserializes from WKT LINESTRING format.
    /// </summary>
    /// <param name="destinationType">The target type (NpgsqlLSeg).</param>
    /// <param name="value">The database value to parse.</param>
    /// <returns>An NpgsqlLSeg instance.</returns>
    public object? Parse(Type destinationType, object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        var valueType = value.GetType();

        // Check if value is already an NpgsqlLSeg (PostgreSQL native)
        if (valueType.Name == "NpgsqlLSeg")
        {
            return value;
        }

        // Parse from string format (other providers)
        var str = value.ToString() ?? string.Empty;

        double x1,
            y1,
            x2,
            y2;

        // Try PostgreSQL native format: "[(x1,y1),(x2,y2)]"
        if (str.StartsWith('[') && str.EndsWith(']'))
        {
            var content = str.Substring(1, str.Length - 2); // Remove "[" and "]"
            var parts = content.Split(PointSeparator, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
            {
                throw new FormatException($"Invalid PostgreSQL lseg format. Expected '[(x1,y1),(x2,y2)]', got: {str}");
            }

            // Parse first point
            var p1Str = parts[0].TrimStart('(');
            var p1Coords = p1Str.Split(',', StringSplitOptions.RemoveEmptyEntries);

            // Parse second point
            var p2Str = parts[1].TrimEnd(')');
            var p2Coords = p2Str.Split(',', StringSplitOptions.RemoveEmptyEntries);

            if (p1Coords.Length != 2 || p2Coords.Length != 2)
            {
                throw new FormatException($"Invalid PostgreSQL lseg format. Expected '[(x1,y1),(x2,y2)]', got: {str}");
            }

            x1 = double.Parse(p1Coords[0], CultureInfo.InvariantCulture);
            y1 = double.Parse(p1Coords[1], CultureInfo.InvariantCulture);
            x2 = double.Parse(p2Coords[0], CultureInfo.InvariantCulture);
            y2 = double.Parse(p2Coords[1], CultureInfo.InvariantCulture);
        }
        // Try WKT LINESTRING format: "LINESTRING(x1 y1, x2 y2)"
        else if (str.StartsWith("LINESTRING(", StringComparison.OrdinalIgnoreCase) && str.EndsWith(')'))
        {
            var coordsStr = str.Substring(11, str.Length - 12); // Remove "LINESTRING(" and ")"
            var pointStrs = coordsStr.Split(',', StringSplitOptions.RemoveEmptyEntries);

            if (pointStrs.Length != 2)
            {
                throw new FormatException($"Invalid WKT lseg format. Expected 'LINESTRING(x1 y1, x2 y2)', got: {str}");
            }

            var p1Coords = pointStrs[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var p2Coords = pointStrs[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (p1Coords.Length != 2 || p2Coords.Length != 2)
            {
                throw new FormatException($"Invalid WKT lseg format. Expected 2 coordinates per point, got: {str}");
            }

            x1 = double.Parse(p1Coords[0], CultureInfo.InvariantCulture);
            y1 = double.Parse(p1Coords[1], CultureInfo.InvariantCulture);
            x2 = double.Parse(p2Coords[0], CultureInfo.InvariantCulture);
            y2 = double.Parse(p2Coords[1], CultureInfo.InvariantCulture);
        }
        else
        {
            throw new FormatException(
                $"Invalid lseg format. Expected '[(x1,y1),(x2,y2)]' or 'LINESTRING(x1 y1, x2 y2)', got: {str}"
            );
        }

        // Create NpgsqlPoint instances for start and end
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

        var start = pointCtor.Invoke(new object[] { x1, y1 });
        var end = pointCtor.Invoke(new object[] { x2, y2 });

        // Create NpgsqlLSeg using reflection
        var lsegType = Type.GetType("NpgsqlTypes.NpgsqlLSeg, Npgsql");
        if (lsegType == null)
        {
            throw new InvalidOperationException("NpgsqlLSeg type not found. Ensure Npgsql package is referenced.");
        }

        var lsegCtor = lsegType.GetConstructor(new[] { pointType, pointType });
        if (lsegCtor == null)
        {
            throw new InvalidOperationException("Could not find appropriate constructor for NpgsqlLSeg.");
        }

        return lsegCtor.Invoke(new[] { start, end });
    }
}
