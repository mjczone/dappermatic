// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text;

namespace MJCZone.DapperMatic;

/// <summary>
/// A descriptor for a SQL type that breaks up the SQL type name into its useful parts, including the base type name, the complete SQL type name, and the numbers extracted from the SQL type name.
/// </summary>
public class SqlTypeDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SqlTypeDescriptor"/> class.
    /// </summary>
    /// <param name="sqlTypeName">The complete SQL type name.</param>
    /// <exception cref="ArgumentException">Thrown when the SQL type name is null or whitespace.</exception>
    public SqlTypeDescriptor(string sqlTypeName)
    {
        if (string.IsNullOrWhiteSpace(sqlTypeName))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(sqlTypeName));
        }

        BaseTypeName = sqlTypeName.DiscardLengthPrecisionAndScaleFromSqlTypeName().ToLowerInvariant();
        SqlTypeName = sqlTypeName;

        // set some of the properties using some rudimentary logic
        // as a starting point using generally known conventions
        // for the most common SQL types
        if (BaseTypeName.Contains("serial", StringComparison.OrdinalIgnoreCase))
        {
            IsAutoIncrementing = true;
        }

        var numbers = sqlTypeName.ExtractNumbers();
        if (numbers.Length > 0)
        {
            if (
                BaseTypeName.Contains("char", StringComparison.OrdinalIgnoreCase)
                || BaseTypeName.Contains("text", StringComparison.OrdinalIgnoreCase)
                || BaseTypeName.Contains("binary", StringComparison.OrdinalIgnoreCase)
            )
            {
                Length = numbers[0];

                if (
                    BaseTypeName.Contains("char", StringComparison.OrdinalIgnoreCase)
                    && !BaseTypeName.Contains("varchar", StringComparison.OrdinalIgnoreCase)
                )
                {
                    IsFixedLength = true;
                }

                if (
                    BaseTypeName.Contains("nchar", StringComparison.OrdinalIgnoreCase)
                    || BaseTypeName.Contains("nvarchar", StringComparison.OrdinalIgnoreCase)
                    || BaseTypeName.Contains("ntext", StringComparison.OrdinalIgnoreCase)
                )
                {
                    IsUnicode = true;
                }
            }
            else
            {
                Precision = numbers[0];
                if (numbers.Length > 1)
                {
                    Scale = numbers[1];
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the base type name of the SQL type.
    /// </summary>
    public string BaseTypeName { get; set; }

    /// <summary>
    /// Gets or sets the complete SQL type name with length, precision and/or scale.
    /// </summary>
    public string SqlTypeName { get; set; }

    /// <summary>
    /// Gets or sets the length of the SQL type.
    /// </summary>
    public int? Length { get; set; }

    /// <summary>
    /// Gets or sets the precision of the SQL type.
    /// </summary>
    public int? Precision { get; set; }

    /// <summary>
    /// Gets or sets the scale of the SQL type.
    /// </summary>
    public int? Scale { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the SQL type is auto-incrementing.
    /// </summary>
    public bool? IsAutoIncrementing { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the SQL type is Unicode.
    /// </summary>
    public bool? IsUnicode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the SQL type has a fixed length.
    /// </summary>
    public bool? IsFixedLength { get; set; }

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        return SqlTypeName;
    }
}
