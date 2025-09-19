// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Converters;

/// <summary>
/// .NET Type to SQL Type converter.
/// </summary>
public class DotnetTypeToSqlTypeConverter
    : IDbTypeConverter<DotnetTypeDescriptor, SqlTypeDescriptor?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DotnetTypeToSqlTypeConverter"/> class.
    /// </summary>
    /// <param name="convertFunc">The function to use for converting <see cref="DotnetTypeDescriptor"/> to <see cref="SqlTypeDescriptor"/>.</param>
    public DotnetTypeToSqlTypeConverter(Func<DotnetTypeDescriptor, SqlTypeDescriptor?> convertFunc)
    {
        ConvertFunc = convertFunc ?? throw new ArgumentNullException(nameof(convertFunc));
    }

    /// <summary>
    /// Gets the function used for converting <see cref="DotnetTypeDescriptor"/> to <see cref="SqlTypeDescriptor"/>.
    /// </summary>
    public Func<DotnetTypeDescriptor, SqlTypeDescriptor?> ConvertFunc { get; }

    /// <summary>
    /// Tries to convert a <see cref="DotnetTypeDescriptor"/> to a <see cref="SqlTypeDescriptor"/>.
    /// </summary>
    /// <param name="from">The <see cref="DotnetTypeDescriptor"/> to convert from.</param>
    /// <param name="to">The converted <see cref="string"/>, if the conversion was successful.</param>
    /// <returns>True if the conversion was successful; otherwise, false.</returns>
    public bool TryConvert(DotnetTypeDescriptor from, out SqlTypeDescriptor? to)
    {
        to = ConvertFunc(from);
        return to != null;
    }
}
