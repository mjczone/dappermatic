// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System;
using System.Data;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Smart type handler for NpgsqlTypes.NpgsqlCidr with provider-specific optimization.
/// PostgreSQL: Native cidr type
/// Others: String serialization (CIDR notation like "192.168.0.0/24")
/// </summary>
public class SmartNpgsqlCidrTypeHandler : SqlMapper.TypeHandler<object>
{
    private static readonly Type? NpgsqlCidrType = Type.GetType("NpgsqlTypes.NpgsqlCidr, Npgsql");

    /// <inheritdoc/>
    public override void SetValue(IDbDataParameter parameter, object? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
            return;
        }

        var paramType = parameter.GetType().FullName ?? string.Empty;

        if (paramType.Contains("Npgsql", StringComparison.Ordinal))
        {
            // PostgreSQL: Native cidr type
            parameter.Value = value;
        }
        else
        {
            // Other providers: String serialization (CIDR notation)
            parameter.Value = value.ToString() ?? string.Empty;
        }
    }

    /// <inheritdoc/>
    public override object? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        // If already NpgsqlCidr type, return as-is
        if (NpgsqlCidrType != null && value.GetType() == NpgsqlCidrType)
        {
            return value;
        }

        // Parse from string (CIDR notation like "192.168.0.0/24")
        var stringValue = value.ToString();
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return null;
        }

        // Use reflection to parse NpgsqlCidr from string
        if (NpgsqlCidrType != null)
        {
            var parseMethod = NpgsqlCidrType.GetMethod("Parse", new[] { typeof(string) });

            if (parseMethod != null)
            {
                try
                {
                    return parseMethod.Invoke(null, new object[] { stringValue });
                }
                catch (Exception ex)
                    when (ex is System.Reflection.TargetInvocationException
                        || ex is ArgumentException
                        || ex is FormatException
                    )
                {
                    // Fall through to return string if parse fails
                }
            }
        }

        // Fallback: return as string if NpgsqlCidr type not available
        return stringValue;
    }
}
