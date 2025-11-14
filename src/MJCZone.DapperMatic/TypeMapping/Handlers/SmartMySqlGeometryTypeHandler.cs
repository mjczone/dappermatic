// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System;
using System.Data;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Smart type handler for MySqlGeometry with provider-specific optimization.
/// MySQL/MariaDB: Native MySqlGeometry type (MySql.Data or MySqlConnector)
/// Others: WKT (Well-Known Text) string format for consistency with DDL reverse engineering
/// </summary>
public class SmartMySqlGeometryTypeHandler : SqlMapper.ITypeHandler
{
    /// <summary>
    /// Sets the parameter value for a MySqlGeometry.
    /// MySQL/MariaDB: Passes geometry directly (provider converts MySqlGeometry to native geometry).
    /// Other providers: Serializes geometry to WKT string.
    /// </summary>
    /// <param name="parameter">The database parameter to set.</param>
    /// <param name="value">The MySqlGeometry value to store.</param>
    public void SetValue(IDbDataParameter parameter, object? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
            return;
        }

        var paramType = parameter.GetType().FullName ?? string.Empty;

        if (
            paramType.Contains("MySql", StringComparison.Ordinal)
            || paramType.Contains("MariaDb", StringComparison.Ordinal)
        )
        {
            // MySQL/MariaDB: Native geometry type handling
            parameter.Value = value;
        }
        else
        {
            // Other providers: WKT string serialization (consistent with DDL fallback)
            var valueType = value.GetType();

            // Try ToString() method to get WKT representation
            var toStringMethod = valueType.GetMethod("ToString", Type.EmptyTypes);
            if (toStringMethod != null)
            {
                var wkt = toStringMethod.Invoke(value, null) as string;
                parameter.Value = (object?)wkt ?? DBNull.Value;
                parameter.DbType = DbType.String;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Could not serialize MySqlGeometry: ToString method not found on type {valueType.FullName}"
                );
            }
        }
    }

    /// <summary>
    /// Parses a database value back to MySqlGeometry.
    /// MySQL/MariaDB: Value is already MySqlGeometry from provider.
    /// Other providers: Deserializes from WKT string.
    /// </summary>
    /// <param name="destinationType">The target type (MySqlGeometry).</param>
    /// <param name="value">The database value to parse.</param>
    /// <returns>A MySqlGeometry instance.</returns>
    public object? Parse(Type destinationType, object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        var valueType = value.GetType();

        // Check if value is already a MySqlGeometry (MySQL/MariaDB native)
        if (valueType.Name.Contains("MySqlGeometry", StringComparison.Ordinal))
        {
            return value;
        }

        // Parse from WKT string (other providers)
        var wkt = value as string;
        if (wkt == null)
        {
            throw new FormatException($"Expected string (WKT) for MySqlGeometry, got: {valueType.FullName}");
        }

        // Create MySqlGeometry using reflection (avoids direct MySql.Data dependency)
        // Try MySql.Data.MySqlClient.MySqlGeometry first
        var geometryType = Type.GetType("MySql.Data.MySqlClient.MySqlGeometry, MySql.Data");
        if (geometryType != null)
        {
            // Try Parse method for WKT string
            var parseMethod = geometryType.GetMethod("Parse", new[] { typeof(string) });
            if (parseMethod != null)
            {
                return parseMethod.Invoke(null, new object[] { wkt });
            }
        }

        // Try MySqlConnector.MySqlGeometry as fallback
        geometryType = Type.GetType("MySqlConnector.MySqlGeometry, MySqlConnector");
        if (geometryType != null)
        {
            // Try Parse method for WKT string
            var parseMethod = geometryType.GetMethod("Parse", new[] { typeof(string) });
            if (parseMethod != null)
            {
                return parseMethod.Invoke(null, new object[] { wkt });
            }
        }

        throw new InvalidOperationException(
            "MySqlGeometry type not found or does not support WKT parsing. Ensure MySql.Data or MySqlConnector package is referenced."
        );
    }
}
