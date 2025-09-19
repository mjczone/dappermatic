// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Providers.Base;

/// <summary>
/// Interface for provider-specific type mapping configuration.
/// This abstracts provider-specific type constants and behavior.
/// </summary>
public interface IProviderTypeMapping
{
    /// <summary>
    /// Gets the provider-specific boolean SQL type.
    /// </summary>
    string BooleanType { get; }

    /// <summary>
    /// Gets the provider-specific enum string SQL type.
    /// </summary>
    string EnumStringType { get; }

    /// <summary>
    /// Gets a value indicating whether this provider supports Unicode by default.
    /// </summary>
    bool IsUnicodeProvider { get; }

    /// <summary>
    /// Gets the mapping of .NET numeric types to provider-specific SQL types.
    /// </summary>
    Dictionary<Type, string> NumericTypeMap { get; }

    /// <summary>
    /// Creates a provider-specific GUID type descriptor.
    /// </summary>
    /// <returns>SQL type descriptor for GUID storage.</returns>
    SqlTypeDescriptor CreateGuidType();

    /// <summary>
    /// Creates a provider-specific object type descriptor.
    /// </summary>
    /// <returns>SQL type descriptor for object storage.</returns>
    SqlTypeDescriptor CreateObjectType();

    /// <summary>
    /// Creates a provider-specific text type descriptor.
    /// </summary>
    /// <param name="descriptor">The .NET type descriptor with length and unicode information.</param>
    /// <returns>SQL type descriptor for text storage.</returns>
    SqlTypeDescriptor CreateTextType(DotnetTypeDescriptor descriptor);

    /// <summary>
    /// Creates a provider-specific DateTime type descriptor.
    /// </summary>
    /// <param name="descriptor">The .NET type descriptor with precision information.</param>
    /// <returns>SQL type descriptor for DateTime storage.</returns>
    SqlTypeDescriptor CreateDateTimeType(DotnetTypeDescriptor descriptor);

    /// <summary>
    /// Creates a provider-specific binary type descriptor.
    /// </summary>
    /// <param name="descriptor">The .NET type descriptor with length and fixed-length information.</param>
    /// <returns>SQL type descriptor for binary storage.</returns>
    SqlTypeDescriptor CreateBinaryType(DotnetTypeDescriptor descriptor);

    /// <summary>
    /// Creates a provider-specific XML type descriptor.
    /// </summary>
    /// <returns>SQL type descriptor for XML storage.</returns>
    SqlTypeDescriptor CreateXmlType();

    /// <summary>
    /// Gets the geometry types supported by this provider for registration.
    /// </summary>
    /// <returns>Array of geometry types supported by this provider.</returns>
    Type[] GetSupportedGeometryTypes();
}