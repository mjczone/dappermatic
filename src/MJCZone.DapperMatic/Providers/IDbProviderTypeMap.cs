// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Providers;

/// <summary>
/// Represents a mapping between .NET types and SQL types.
/// </summary>
public interface IDbProviderTypeMap
{
    /// <summary>
    /// Converts the SQL type name to a .NET type with appropriate type property
    /// descriptors, such as length, precision, and scale among other things.
    /// </summary>
    /// <param name="sqlTypeName">The sql type name including the length, precision, and/or scale (e.g., nvarchar(255), varchar(max), decimal(16,4), etc...)</param>
    /// <param name="dotnetTypeDescriptor">A corresponding .NET type descriptor object, or null.</param>
    /// <returns>true/false.</returns>
    bool TryGetDotnetTypeDescriptorMatchingFullSqlTypeName(
        string sqlTypeName,
        out DotnetTypeDescriptor? dotnetTypeDescriptor
    );

    /// <summary>
    /// Converts the SQL type descriptor to a .NET type with appropriate type property
    /// descriptors, such as length, precision, and scale among other things.
    /// </summary>
    /// <param name="sqlTypeDescriptor">The sql type descriptor including the length, precision, and/or scale among other things.</param>
    /// <param name="dotnetTypeDescriptor">A corresponding .NET type descriptor object, or null.</param>
    /// <returns>true/false.</returns>
    bool TryGetDotnetTypeDescriptorMatchingFullSqlTypeName(
        SqlTypeDescriptor sqlTypeDescriptor,
        out DotnetTypeDescriptor? dotnetTypeDescriptor
    );

    /// <summary>
    /// Converts the .NET type to a SQL type with appropriate type property
    /// descriptors, such as length, precision, and scale among other things.
    /// </summary>
    /// <param name="type">The .NET type to convert to a SQL type.</param>
    /// <param name="sqlTypeDescriptor">A corresponding SQL type descriptor object, or null.</param>
    /// <returns>true/false.</returns>
    bool TryGetProviderSqlTypeMatchingDotnetType(
        Type type,
        out SqlTypeDescriptor? sqlTypeDescriptor
    );

    /// <summary>
    /// Converts the .NET type descriptor to a SQL type with appropriate type property
    /// descriptors, such as length, precision, and scale among other things.
    /// </summary>
    /// <param name="dotnetTypeDescriptor">The .NET type descriptor to convert to a SQL type.</param>
    /// <param name="sqlTypeDescriptor">A corresponding SQL type descriptor object, or null.</param>
    /// <returns>true/false.</returns>
    bool TryGetProviderSqlTypeMatchingDotnetType(
        DotnetTypeDescriptor dotnetTypeDescriptor,
        out SqlTypeDescriptor? sqlTypeDescriptor
    );
}
