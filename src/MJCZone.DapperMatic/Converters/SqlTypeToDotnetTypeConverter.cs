// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Converters;

/// <summary>
/// Converts a <see cref="SqlTypeDescriptor"/> to a <see cref="DotnetTypeDescriptor"/>.
/// </summary>
public class SqlTypeToDotnetTypeConverter
    : IDbTypeConverter<SqlTypeDescriptor, DotnetTypeDescriptor>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SqlTypeToDotnetTypeConverter"/> class.
    /// </summary>
    /// <param name="convertFunc">The function to use for converting <see cref="SqlTypeDescriptor"/> to <see cref="DotnetTypeDescriptor"/>.</param>
    public SqlTypeToDotnetTypeConverter(Func<SqlTypeDescriptor, DotnetTypeDescriptor?> convertFunc)
    {
        ConvertFunc = convertFunc ?? throw new ArgumentNullException(nameof(convertFunc));
    }

    /// <summary>
    /// Gets the function used for converting <see cref="SqlTypeDescriptor"/> to <see cref="DotnetTypeDescriptor"/>.
    /// </summary>
    public Func<SqlTypeDescriptor, DotnetTypeDescriptor?> ConvertFunc { get; }

    /// <summary>
    /// Tries to convert a <see cref="SqlTypeDescriptor"/> to a <see cref="DotnetTypeDescriptor"/>.
    /// </summary>
    /// <param name="from">The <see cref="SqlTypeDescriptor"/> to convert from.</param>
    /// <param name="to">The converted <see cref="DotnetTypeDescriptor"/>, if the conversion was successful.</param>
    /// <returns>True if the conversion was successful; otherwise, false.</returns>
    public bool TryConvert(SqlTypeDescriptor from, out DotnetTypeDescriptor? to)
    {
        to = ConvertFunc(from);
        return to != null;
    }
}
