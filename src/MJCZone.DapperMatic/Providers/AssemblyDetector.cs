// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System;

namespace MJCZone.DapperMatic.Providers;

/// <summary>
/// Detects availability of optional assemblies for type mapping.
/// Uses lazy initialization with caching for performance.
/// </summary>
public static class AssemblyDetector
{
    private static bool? _hasMySqlData;
    private static bool? _hasSqlServerTypes;
    private static bool? _hasNetTopologySuite;

    /// <summary>
    /// Gets a value indicating whether MySql.Data assembly is available.
    /// Used for MySqlGeometry type support.
    /// </summary>
    public static bool HasMySqlData
    {
        get
        {
            if (!_hasMySqlData.HasValue)
            {
                _hasMySqlData = DetectType("MySql.Data.Types.MySqlGeometry, MySql.Data");
            }
            return _hasMySqlData.Value;
        }
    }

    /// <summary>
    /// Gets a value indicating whether Microsoft.SqlServer.Types assembly is available.
    /// Note: SQL Server native spatial types (SqlGeography, SqlGeometry, SqlHierarchyId) are NOT currently supported
    /// due to platform-specific native library dependencies. Use NetTopologySuite for cross-platform spatial support.
    /// </summary>
    public static bool HasSqlServerTypes
    {
        get
        {
            if (!_hasSqlServerTypes.HasValue)
            {
                _hasSqlServerTypes = DetectType("Microsoft.SqlServer.Types.SqlGeography, Microsoft.SqlServer.Types");
            }
            return _hasSqlServerTypes.Value;
        }
    }

    /// <summary>
    /// Gets a value indicating whether NetTopologySuite assembly is available.
    /// Used for Geometry, Point, LineString, Polygon type support.
    /// </summary>
    public static bool HasNetTopologySuite
    {
        get
        {
            if (!_hasNetTopologySuite.HasValue)
            {
                _hasNetTopologySuite = DetectType("NetTopologySuite.Geometries.Geometry, NetTopologySuite");
            }
            return _hasNetTopologySuite.Value;
        }
    }

    /// <summary>
    /// Resets cached assembly detection results.
    /// Useful for testing scenarios.
    /// </summary>
    public static void Reset()
    {
        _hasMySqlData = null;
        _hasSqlServerTypes = null;
        _hasNetTopologySuite = null;
    }

    private static bool DetectType(string assemblyQualifiedName)
    {
        var type = Type.GetType(assemblyQualifiedName, throwOnError: false);
        return type != null;
    }
}
