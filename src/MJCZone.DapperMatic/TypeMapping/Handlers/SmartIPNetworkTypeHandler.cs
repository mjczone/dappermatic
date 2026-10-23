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
/// Smart type handler for System.Net.IPNetwork with provider-specific optimization.
/// PostgreSQL: Native cidr type, which Npgsql 10 and later read as IPNetwork
/// Others: String serialization (CIDR notation like "192.168.0.0/24")
/// </summary>
public class SmartIPNetworkTypeHandler : SqlMapper.TypeHandler<IPNetwork>
{
    /// <inheritdoc/>
    public override void SetValue(IDbDataParameter parameter, IPNetwork value)
    {
        var paramType = parameter.GetType().FullName ?? string.Empty;

        if (paramType.Contains("Npgsql", StringComparison.Ordinal))
        {
            // PostgreSQL: Native cidr type
            parameter.Value = value;
        }
        else
        {
            // Other providers: String serialization (CIDR notation)
            parameter.Value = value.ToString();
        }
    }

    /// <inheritdoc/>
    public override IPNetwork Parse(object value)
    {
        if (value is IPNetwork network)
        {
            return network;
        }

        // A string from other providers, or an NpgsqlCidr from Npgsql before version 10; both print CIDR notation.
        return IPNetwork.Parse(value.ToString() ?? string.Empty);
    }
}
