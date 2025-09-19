// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.AspNetCore.Extensions;

/// <summary>
/// Extension methods for converting internal DataTypeInfo to public ProviderDataType.
/// </summary>
internal static class DataTypeInfoExtensions
{
    /// <summary>
    /// Converts a DataTypeInfo to a ProviderDataType.
    /// </summary>
    /// <param name="dataTypeInfo">The internal data type info.</param>
    /// <returns>The public provider data type.</returns>
    public static ProviderDataType ToProviderDataType(this DataTypeInfo dataTypeInfo)
    {
        return new ProviderDataType
        {
            DataType = dataTypeInfo.DataType,
            Aliases = dataTypeInfo.Aliases ?? [],
            IsCommon = dataTypeInfo.IsCommon,
            IsCustom = dataTypeInfo.IsCustom,
            SupportsLength = dataTypeInfo.SupportsLength,
            MinLength = dataTypeInfo.MinLength,
            MaxLength = dataTypeInfo.MaxLength,
            DefaultLength = dataTypeInfo.DefaultLength,
            SupportsPrecision = dataTypeInfo.SupportsPrecision,
            MinPrecision = dataTypeInfo.MinPrecision,
            MaxPrecision = dataTypeInfo.MaxPrecision,
            DefaultPrecision = dataTypeInfo.DefaultPrecision,
            SupportsScale = dataTypeInfo.SupportsScale,
            MinScale = dataTypeInfo.MinScale,
            MaxScale = dataTypeInfo.MaxScale,
            DefaultScale = dataTypeInfo.DefaultScale,
            Category = dataTypeInfo.Category.ToString(),
            Description = dataTypeInfo.Description,
            Examples = dataTypeInfo.Examples,
        };
    }

    /// <summary>
    /// Converts a collection of DataTypeInfo to ProviderDataType.
    /// </summary>
    /// <param name="dataTypeInfos">The collection of internal data type infos.</param>
    /// <returns>A collection of public provider data types.</returns>
    public static IEnumerable<ProviderDataType> ToProviderDataTypes(
        this IEnumerable<DataTypeInfo> dataTypeInfos
    )
    {
        return dataTypeInfos.Select(ToProviderDataType);
    }
}
