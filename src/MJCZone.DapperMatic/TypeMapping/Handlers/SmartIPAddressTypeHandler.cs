// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System;
using System.Data;
using System.Net;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Smart type handler for System.Net.IPAddress with provider-specific optimization.
/// PostgreSQL: Native inet type
/// Others: String serialization
/// </summary>
public class SmartIPAddressTypeHandler : SqlMapper.TypeHandler<IPAddress>
{
    /// <inheritdoc/>
    public override void SetValue(IDbDataParameter parameter, IPAddress? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
            return;
        }

        var paramType = parameter.GetType().FullName ?? string.Empty;

        if (paramType.Contains("Npgsql", StringComparison.Ordinal))
        {
            // PostgreSQL: Native inet type
            parameter.Value = value;
        }
        else
        {
            // Other providers: String serialization
            parameter.Value = value.ToString();
        }
    }

    /// <inheritdoc/>
    public override IPAddress? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        if (value is IPAddress ipAddress)
        {
            return ipAddress;
        }

        var stringValue = value.ToString();
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return null;
        }

        return IPAddress.Parse(stringValue);
    }
}
