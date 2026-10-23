// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Globalization;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Reads a <see cref="Guid"/> back from text or bytes, which is how SQLite drivers return one.
/// </summary>
/// <remarks>
/// Dapper writes a Guid itself, so only <see cref="Parse"/> is used.
/// </remarks>
internal sealed class SmartGuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    /// <inheritdoc />
    public override void SetValue(IDbDataParameter parameter, Guid value) => parameter.Value = value;

    /// <inheritdoc />
    public override Guid Parse(object value) =>
        value switch
        {
            Guid guid => guid,
            byte[] { Length: 16 } bytes => new Guid(bytes),
            _ => Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty),
        };
}

/// <summary>
/// Reads a <see cref="TimeSpan"/> back from text, or from the <see cref="DateTime"/> that System.Data.SQLite
/// returns for a <c>time</c> column.
/// </summary>
/// <remarks>
/// Dapper writes a TimeSpan itself, so only <see cref="Parse"/> is used.
/// </remarks>
internal sealed class SmartTimeSpanTypeHandler : SqlMapper.TypeHandler<TimeSpan>
{
    /// <inheritdoc />
    public override void SetValue(IDbDataParameter parameter, TimeSpan value) => parameter.Value = value;

    /// <inheritdoc />
    public override TimeSpan Parse(object value) =>
        value switch
        {
            TimeSpan span => span,
            DateTime dateTime => dateTime.TimeOfDay,
            _ => TimeSpan.Parse(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, CultureInfo.InvariantCulture),
        };
}

/// <summary>
/// Reads a <see cref="decimal"/> back from whatever number the driver returns. SQLite stores a value in a NUMERIC
/// column as an integer when it has no fraction and as a real when it has one, so the rows of one column can come back
/// as different types, and Dapper reads every row of a result as the type it saw first.
/// </summary>
/// <remarks>
/// Dapper writes a decimal itself, so only <see cref="Parse"/> is used.
/// </remarks>
internal sealed class SmartDecimalTypeHandler : SqlMapper.TypeHandler<decimal>
{
    /// <inheritdoc />
    public override void SetValue(IDbDataParameter parameter, decimal value) => parameter.Value = value;

    /// <inheritdoc />
    public override decimal Parse(object value) =>
        value switch
        {
            decimal number => number,
            string text => decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture),
            _ => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
        };
}
