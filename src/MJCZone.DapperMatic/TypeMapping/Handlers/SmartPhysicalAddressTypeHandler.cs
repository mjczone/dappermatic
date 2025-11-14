// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System;
using System.Data;
using System.Net.NetworkInformation;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Smart type handler for System.Net.NetworkInformation.PhysicalAddress with provider-specific optimization.
/// PostgreSQL: Native macaddr type
/// Others: String serialization
/// </summary>
public class SmartPhysicalAddressTypeHandler : SqlMapper.TypeHandler<PhysicalAddress>
{
    /// <inheritdoc/>
    public override void SetValue(IDbDataParameter parameter, PhysicalAddress? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
            return;
        }

        var paramType = parameter.GetType().FullName ?? string.Empty;

        if (paramType.Contains("Npgsql", StringComparison.Ordinal))
        {
            // PostgreSQL: Native macaddr type
            parameter.Value = value;
        }
        else
        {
            // Other providers: String serialization (format: 00:11:22:33:44:55)
            var bytes = value.GetAddressBytes();
            parameter.Value = BitConverter.ToString(bytes).Replace('-', ':');
        }
    }

    /// <inheritdoc/>
    public override PhysicalAddress? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        if (value is PhysicalAddress physicalAddress)
        {
            return physicalAddress;
        }

        var stringValue = value.ToString();
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return null;
        }

        // Parse format: 00:11:22:33:44:55 or 00-11-22-33-44-55
        return PhysicalAddress.Parse(
            stringValue
                .Replace(":", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
        );
    }
}
