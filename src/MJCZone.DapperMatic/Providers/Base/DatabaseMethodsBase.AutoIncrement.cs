// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Providers.Base;

public abstract partial class DatabaseMethodsBase
{
    /// <summary>
    /// Determines if a column should be marked as auto-increment based on various indicators.
    /// </summary>
    /// <param name="column">The column to check.</param>
    /// <param name="providerSpecificMetadata">Provider-specific metadata that might indicate auto-increment (e.g., is_identity, extra field).</param>
    /// <param name="sqlTypeName">The SQL type name of the column.</param>
    /// <returns>True if the column should be marked as auto-increment, false otherwise.</returns>
    protected virtual bool DetermineIsAutoIncrement(
        DmColumn column,
        object? providerSpecificMetadata = null,
        string? sqlTypeName = null)
    {
        // 1. Check if already explicitly set
        if (column.IsAutoIncrement)
        {
            return true;
        }

        // 2. Check SQL type name for common auto-increment patterns
        if (!string.IsNullOrWhiteSpace(sqlTypeName))
        {
            var sqlTypeDescriptor = new SqlTypeDescriptor(sqlTypeName);
            if (sqlTypeDescriptor.IsAutoIncrementing == true)
            {
                return true;
            }

            // Additional patterns not caught by SqlTypeDescriptor
            var lowerTypeName = sqlTypeName.ToLowerInvariant();
            if (lowerTypeName.Contains("identity", StringComparison.OrdinalIgnoreCase) ||
                lowerTypeName.Contains("autoincrement", StringComparison.OrdinalIgnoreCase) ||
                lowerTypeName.Contains("auto_increment", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // 3. Check provider data type from column
        var providerDataType = column.GetProviderDataType(ProviderType);
        if (!string.IsNullOrWhiteSpace(providerDataType))
        {
            var lowerProviderType = providerDataType.ToLowerInvariant();
            if (lowerProviderType.Contains("serial", StringComparison.OrdinalIgnoreCase) ||
                lowerProviderType.Contains("identity", StringComparison.OrdinalIgnoreCase) ||
                lowerProviderType.Contains("autoincrement", StringComparison.OrdinalIgnoreCase) ||
                lowerProviderType.Contains("auto_increment", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // 4. Check type map for auto-increment flag
        try
        {
            var dotnetTypeDescriptor = GetDotnetTypeFromSqlType(
                providerDataType ?? sqlTypeName ?? string.Empty);

            if (dotnetTypeDescriptor?.IsAutoIncrementing == true)
            {
                return true;
            }
        }
        catch (ArgumentException)
        {
            // Type mapping might fail for custom types, continue with other checks
        }

        // 5. Provider-specific checks (to be overridden by providers)
        if (providerSpecificMetadata != null)
        {
            return CheckProviderSpecificAutoIncrement(providerSpecificMetadata);
        }

        return false;
    }

    /// <summary>
    /// Checks provider-specific metadata for auto-increment indicators.
    /// Override this method in provider implementations to handle provider-specific logic.
    /// </summary>
    /// <param name="metadata">Provider-specific metadata object.</param>
    /// <returns>True if the metadata indicates auto-increment, false otherwise.</returns>
    protected virtual bool CheckProviderSpecificAutoIncrement(object metadata)
    {
        // Base implementation - providers should override this
        return false;
    }
}