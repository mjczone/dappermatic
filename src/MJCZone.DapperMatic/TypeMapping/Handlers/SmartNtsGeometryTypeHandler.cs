// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System;
using System.Data;
using System.Reflection;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Smart type handler for NetTopologySuite geometry types using WKT format for cross-database compatibility.
/// All providers: WKT (Well-Known Text) format via ToString()/Parse
/// </summary>
/// <remarks>
/// This handler uses reflection to work with NetTopologySuite types without taking a direct
/// dependency. The consuming application must reference NetTopologySuite for this handler
/// to function. Uses WKT text format for all providers (human-readable, no extra dependencies).
/// This approach avoids requiring provider-specific NTS packages (Npgsql.NetTopologySuite, MySqlConnector.NetTopologySuite).
/// </remarks>
public class SmartNtsGeometryTypeHandler : SqlMapper.ITypeHandler
{
    // Lazy-loaded NTS types via reflection
    private static Type? _geometryType;
    private static Type? _wkbReaderType;
    private static Type? _wkbWriterType;
    private static object? _wkbReaderInstance;
    private static object? _wkbWriterInstance;
    private static MethodInfo? _wkbReadMethod;
    private static MethodInfo? _wkbWriteMethod;

    static SmartNtsGeometryTypeHandler()
    {
        InitializeNtsTypes();
    }

    /// <summary>
    /// Sets the parameter value for a NetTopologySuite geometry.
    /// All providers: Serializes geometry to WKT text via ToString().
    /// </summary>
    /// <param name="parameter">The database parameter to set.</param>
    /// <param name="value">The NTS Geometry value to store.</param>
    public void SetValue(IDbDataParameter parameter, object? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
            return;
        }

        // All providers: Use WKT text format (ToString() produces WKT)
        // This is human-readable, cross-database compatible, and doesn't require provider-specific NTS packages
        var wkt = value.ToString();
        if (string.IsNullOrEmpty(wkt))
        {
            parameter.Value = DBNull.Value;
            return;
        }

        parameter.Value = wkt;
        parameter.DbType = DbType.String;
    }

    /// <summary>
    /// Parses a database value back to a NetTopologySuite geometry.
    /// All providers: Deserializes from WKT text string.
    /// </summary>
    /// <param name="destinationType">The target type (NTS Geometry or derived type).</param>
    /// <param name="value">The database value to parse.</param>
    /// <returns>An NTS Geometry instance (Point, LineString, Polygon, etc.).</returns>
    public object? Parse(Type destinationType, object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        var valueType = value.GetType();

        // Check if value is already an NTS Geometry (shouldn't happen with WKT storage, but handle for safety)
        if (_geometryType != null && _geometryType.IsAssignableFrom(valueType))
        {
            return value;
        }

        // Parse from WKT text string (all providers)
        if (value is string wkt && !string.IsNullOrEmpty(wkt))
        {
            return ParseWkt(wkt);
        }

        // Fall back to WKB byte array if available (for backwards compatibility)
        if (value is byte[] wkb && wkb.Length > 0)
        {
            if (_wkbReaderInstance == null || _wkbReadMethod == null)
            {
                throw new InvalidOperationException(
                    "NetTopologySuite.IO.WKBReader not found. Ensure NetTopologySuite.IO package is referenced for WKB support."
                );
            }

            // Call WKBReader.Read(byte[]) to get Geometry
            return _wkbReadMethod.Invoke(_wkbReaderInstance, new object[] { wkb });
        }

        throw new FormatException(
            $"Expected WKT string or NTS Geometry for NetTopologySuite type, got: {valueType.FullName}"
        );
    }

    private static object? ParseWkt(string wkt)
    {
        // Use NTS WKTReader via reflection to parse WKT text
        var wktReaderType = Type.GetType("NetTopologySuite.IO.WKTReader, NetTopologySuite", throwOnError: false);
        if (wktReaderType == null)
        {
            throw new InvalidOperationException(
                "NetTopologySuite.IO.WKTReader not found. Ensure NetTopologySuite package is referenced."
            );
        }

        // Create WKTReader instance
        var ctor = wktReaderType.GetConstructor(Type.EmptyTypes);
        if (ctor == null)
        {
            throw new InvalidOperationException("Could not find constructor for WKTReader.");
        }

        var wktReaderInstance = ctor.Invoke(null);

        // Get Read(string) method
        var readMethod = wktReaderType.GetMethod("Read", new[] { typeof(string) });
        if (readMethod == null)
        {
            throw new InvalidOperationException("Could not find Read method on WKTReader.");
        }

        // Call WKTReader.Read(string) to get Geometry
        return readMethod.Invoke(wktReaderInstance, new object[] { wkt });
    }

    private static void InitializeNtsTypes()
    {
        // Detect NetTopologySuite.Geometries.Geometry
        _geometryType = Type.GetType("NetTopologySuite.Geometries.Geometry, NetTopologySuite", throwOnError: false);
        if (_geometryType == null)
        {
            return; // NTS not available, handler will throw on use
        }

        // Detect NetTopologySuite.IO.WKBReader
        _wkbReaderType = Type.GetType("NetTopologySuite.IO.WKBReader, NetTopologySuite", throwOnError: false);
        if (_wkbReaderType == null)
        {
            return; // NTS not available, handler will throw on use
        }

        // Detect NetTopologySuite.IO.WKBWriter
        _wkbWriterType = Type.GetType("NetTopologySuite.IO.WKBWriter, NetTopologySuite", throwOnError: false);
        if (_wkbWriterType == null)
        {
            return; // NTS not available, handler will throw on use
        }

        // Create WKBReader instance
        var ctor = _wkbReaderType.GetConstructor(Type.EmptyTypes);
        if (ctor != null)
        {
            _wkbReaderInstance = ctor.Invoke(null);

            // Get Read(byte[]) method
            _wkbReadMethod = _wkbReaderType.GetMethod("Read", new[] { typeof(byte[]) });
        }

        // Create WKBWriter instance
        ctor = _wkbWriterType.GetConstructor(Type.EmptyTypes);
        if (ctor != null)
        {
            _wkbWriterInstance = ctor.Invoke(null);

            // Get Write(Geometry) method
            _wkbWriteMethod = _wkbWriterType.GetMethod("Write", new[] { _geometryType });
        }
    }
}
