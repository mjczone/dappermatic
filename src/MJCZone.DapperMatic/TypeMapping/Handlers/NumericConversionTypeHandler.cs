// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Globalization;
using System.Numerics;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Writes numbers that drivers do not accept as parameters as the type of the column DDL creates for them, and
/// reads them back from whatever the driver returns.
/// </summary>
/// <remarks>
/// SQL Server, PostgreSQL and SQLite have no unsigned types, so <see cref="sbyte"/>, <see cref="ushort"/>,
/// <see cref="uint"/> and <see cref="ulong"/> are widened to the next signed type, or to a decimal for
/// <see cref="ulong"/>; MySQL has unsigned types and receives them as they are. <see cref="Half"/> is written as a
/// <see cref="float"/>. <see cref="Int128"/>, <see cref="UInt128"/> and <see cref="BigInteger"/> are stored as their
/// digits.
/// </remarks>
/// <typeparam name="T">The numeric type.</typeparam>
internal sealed class NumericConversionTypeHandler<T> : SqlMapper.TypeHandler<T>
    where T : struct
{
    /// <inheritdoc />
    public override void SetValue(IDbDataParameter parameter, T value)
    {
        parameter.Value = value switch
        {
            sbyte or ushort or uint or ulong when ProviderParameter.IsMySql(parameter) => value,
            sbyte v => (short)v,
            ushort v => (int)v,
            uint v => (long)v,
            ulong v when ProviderParameter.IsSqlite(parameter) && v <= long.MaxValue => (long)v,
            ulong v when ProviderParameter.IsSqlite(parameter) => v.ToString(CultureInfo.InvariantCulture),
            ulong v => (decimal)v,
            Half v => (float)v,
            IFormattable digits => digits.ToString(null, CultureInfo.InvariantCulture),
            _ => value,
        };
    }

    /// <inheritdoc />
    public override T Parse(object value)
    {
        if (value is T typed)
        {
            return typed;
        }

        var culture = CultureInfo.InvariantCulture;
        object result = typeof(T) switch
        {
            Type t when t == typeof(sbyte) => Convert.ToSByte(value, culture),
            Type t when t == typeof(ushort) => Convert.ToUInt16(value, culture),
            Type t when t == typeof(uint) => Convert.ToUInt32(value, culture),
            Type t when t == typeof(ulong) => Convert.ToUInt64(value, culture),
            Type t when t == typeof(Half) => (Half)Convert.ToSingle(value, culture),
            Type t when t == typeof(Int128) => Int128.Parse(Digits(value), culture),
            Type t when t == typeof(UInt128) => UInt128.Parse(Digits(value), culture),
            Type t when t == typeof(BigInteger) => BigInteger.Parse(Digits(value), culture),
            _ => throw new NotSupportedException($"{typeof(T).Name} is not a type this handler converts."),
        };

        return (T)result;
    }

    private static string Digits(object value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0";
}
