// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.NetworkInformation;
using MJCZone.DapperMatic.Providers.Base;

namespace MJCZone.DapperMatic.Providers;

/// <summary>
/// Base class for provider-specific type mapping configurations.
/// </summary>
public abstract class ProviderTypeMappingBase : IProviderTypeMapping
{
    /// <inheritdoc />
    public abstract string BooleanType { get; }

    /// <inheritdoc />
    public virtual bool IsUnicodeProvider => false; // FALSE if the provider does not use Unicode by default

    /// <inheritdoc />
    public abstract Dictionary<Type, string> NumericTypeMap { get; }

    /// <inheritdoc />
    public virtual SqlTypeDescriptor CreateGuidType()
    {
        return CreateStringTypeInternal(
            length: TypeMappingDefaults.GuidStringLength,
            isUnicode: false,
            isFixedLength: true
        );
    }

    /// <inheritdoc />
    public virtual SqlTypeDescriptor CreateCharType(DotnetTypeDescriptor descriptor)
    {
        return CreateStringTypeInternal(
            length: 1,
            isUnicode: descriptor.IsUnicode.GetValueOrDefault(false),
            isFixedLength: true
        );
    }

    /// <inheritdoc />
    public abstract SqlTypeDescriptor CreateObjectType(DotnetTypeDescriptor descriptor);

    /// <inheritdoc />
    public virtual SqlTypeDescriptor CreateTextType(DotnetTypeDescriptor descriptor)
    {
        // Length is NOT supported by SQLite; however,
        // using nvarchar and varchar gives DapperMatic a better chance of mapping the
        // correct type when reading the schema
        return CreateStringTypeInternal(
            length: descriptor.Length.GetValueOrDefault(TypeMappingDefaults.DefaultStringLength),
            isUnicode: descriptor.IsUnicode.GetValueOrDefault(false),
            isFixedLength: descriptor.IsFixedLength.GetValueOrDefault(false)
        );
    }

    /// <inheritdoc />
    public abstract SqlTypeDescriptor CreateDateTimeType(DotnetTypeDescriptor descriptor);

    /// <inheritdoc />
    public abstract SqlTypeDescriptor CreateBinaryType(DotnetTypeDescriptor descriptor);

    /// <inheritdoc />
    public abstract SqlTypeDescriptor CreateXmlType(DotnetTypeDescriptor descriptor);

    /// <inheritdoc />
    public virtual SqlTypeDescriptor CreateNetworkType(DotnetTypeDescriptor descriptor)
    {
        return descriptor.DotnetType switch
        {
            Type t when t == typeof(IPAddress) => CreateStringTypeInternal(
                length: descriptor.Length.GetValueOrDefault(45),
                isUnicode: descriptor.IsUnicode.GetValueOrDefault(false),
                isFixedLength: descriptor.IsFixedLength.GetValueOrDefault(false)
            ),
            Type t when t == typeof(PhysicalAddress) => CreateStringTypeInternal(
                length: descriptor.Length.GetValueOrDefault(17),
                isUnicode: descriptor.IsUnicode.GetValueOrDefault(false),
                isFixedLength: descriptor.IsFixedLength.GetValueOrDefault(false)
            ),
            _ => CreateStringTypeInternal(
                length: descriptor.Length.GetValueOrDefault(50),
                isUnicode: descriptor.IsUnicode.GetValueOrDefault(false),
                isFixedLength: descriptor.IsFixedLength.GetValueOrDefault(false)
            ),
        };
    }

    /// <summary>
    /// Creates a SqlTypeDescriptor for string/text types with consistent length and unicode handling.
    /// </summary>
    /// <param name="length">The length.</param>
    /// <param name="isUnicode">Whether the type supports unicode characters.</param>
    /// <param name="isFixedLength">Whether the type is fixed-length.</param>
    /// <returns>A SqlTypeDescriptor with properly formatted SQL type name and metadata.</returns>
    protected abstract SqlTypeDescriptor CreateStringTypeInternal(
        int length,
        bool isUnicode = false,
        bool isFixedLength = false
    );
}
