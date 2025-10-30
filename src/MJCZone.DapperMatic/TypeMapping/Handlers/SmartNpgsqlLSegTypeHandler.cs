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

        // Parse from WKT LINESTRING format (other providers)
        var wkt = value.ToString() ?? string.Empty;

        // Expected format: "LINESTRING(x1 y1, x2 y2)"
#pragma warning disable CA1865 // Use char overload (no StringComparison overload available for char)
        if (
            !wkt.StartsWith("LINESTRING(", StringComparison.OrdinalIgnoreCase)
            || !wkt.EndsWith(")", StringComparison.Ordinal)
        )
#pragma warning restore CA1865
        {
            throw new FormatException(
                $"Invalid WKT format for line segment. Expected 'LINESTRING(x1 y1, x2 y2)', got: {wkt}"
            );
        }

        var coordsStr = wkt.Substring(11, wkt.Length - 12); // Remove "LINESTRING(" and ")"
        var pointStrs = coordsStr.Split(',', StringSplitOptions.RemoveEmptyEntries);

        if (pointStrs.Length != 2)
        {
            throw new FormatException(
                $"Invalid WKT format for line segment. Expected 2 points, got: {pointStrs.Length}"
            );
        }

        var p1Coords = pointStrs[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var p2Coords = pointStrs[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (p1Coords.Length != 2 || p2Coords.Length != 2)
        {
            throw new FormatException("Invalid coordinate format in line segment WKT.");
        }

        var x1 = double.Parse(p1Coords[0], CultureInfo.InvariantCulture);
        var y1 = double.Parse(p1Coords[1], CultureInfo.InvariantCulture);
        var x2 = double.Parse(p2Coords[0], CultureInfo.InvariantCulture);
        var y2 = double.Parse(p2Coords[1], CultureInfo.InvariantCulture);

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
