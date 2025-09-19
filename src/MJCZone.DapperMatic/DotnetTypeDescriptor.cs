// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text;

namespace MJCZone.DapperMatic;

/// <summary>
/// Describes a .NET type with its SQL type properties.
/// </summary>
public class DotnetTypeDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DotnetTypeDescriptor"/> class.
    /// </summary>
    /// <param name="dotnetType">The .NET type.</param>
    /// <param name="length">The length of the type.</param>
    /// <param name="precision">The precision of the type.</param>
    /// <param name="scale">The scale of the type.</param>
    /// <param name="isAutoIncrementing">Indicates if the type is auto-incrementing.</param>
    /// <param name="isUnicode">Indicates if the type is Unicode.</param>
    /// <param name="isFixedLength">Indicates if the type has a fixed length.</param>
    public DotnetTypeDescriptor(
        Type dotnetType,
        int? length = null,
        int? precision = null,
        int? scale = null,
        bool? isAutoIncrementing = null,
        bool? isUnicode = null,
        bool? isFixedLength = null
    )
    {
        DotnetType =
            dotnetType?.OrUnderlyingTypeIfNullable()
            ?? throw new ArgumentNullException(nameof(dotnetType));
        Length = length;
        Precision = precision;
        Scale = scale;
        IsAutoIncrementing = isAutoIncrementing;
        IsUnicode = isUnicode;
        IsFixedLength = isFixedLength;
    }

    /// <summary>
    /// Gets or sets the .NET type.
    /// </summary>
    public Type DotnetType { get; set; }

    /// <summary>
    /// Gets or sets the length of the type.
    /// </summary>
    public int? Length { get; set; }

    /// <summary>
    /// Gets or sets the precision of the type.
    /// </summary>
    public int? Precision { get; set; }

    /// <summary>
    /// Gets or sets the scale of the type.
    /// </summary>
    public int? Scale { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the type is auto-incrementing.
    /// </summary>
    public bool? IsAutoIncrementing { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the type is Unicode.
    /// </summary>
    public bool? IsUnicode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the type has a fixed length.
    /// </summary>
    public bool? IsFixedLength { get; set; }

    /// <summary>
    /// Describes the object as a string.
    /// </summary>
    /// <returns>The string representation of the object.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(DotnetType.GetFriendlyName());
        if (Length.GetValueOrDefault(0) > 0)
        {
            sb.Append($" length({Length})");
        }

        if (Precision.GetValueOrDefault(0) > 0)
        {
            if (Scale.GetValueOrDefault(0) > 0)
            {
                sb.Append($" precision({Precision},{Scale})");
            }
            else
            {
                sb.Append($" precision({Precision})");
            }
        }

        if (IsAutoIncrementing.GetValueOrDefault(false))
        {
            sb.Append(" auto_increment");
        }

        if (IsUnicode.GetValueOrDefault(false))
        {
            sb.Append(" unicode");
        }

        return sb.ToString();
    }
}
