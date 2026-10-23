// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Globalization;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Stores a value as its text, on every provider: for types such as <see cref="Uri"/> and <see cref="Version"/>
/// that no driver accepts as a parameter.
/// </summary>
/// <typeparam name="T">The type stored.</typeparam>
internal sealed class StringValueTypeHandler<T> : SqlMapper.TypeHandler<T>
{
    private readonly Func<T, string> _format;
    private readonly Func<string, T> _parse;

    /// <summary>
    /// Initializes a new instance of the <see cref="StringValueTypeHandler{T}"/> class.
    /// </summary>
    /// <param name="format">Writes a value as text.</param>
    /// <param name="parse">Reads a value back from its text.</param>
    public StringValueTypeHandler(Func<T, string> format, Func<string, T> parse)
    {
        _format = format;
        _parse = parse;
    }

    /// <inheritdoc />
    public override void SetValue(IDbDataParameter parameter, T? value)
    {
        parameter.Value = value is null ? DBNull.Value : _format(value);
    }

    /// <inheritdoc />
    public override T? Parse(object value) =>
        value switch
        {
            null or DBNull => default,
            T typed => typed,
            _ => _parse(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty),
        };
}
