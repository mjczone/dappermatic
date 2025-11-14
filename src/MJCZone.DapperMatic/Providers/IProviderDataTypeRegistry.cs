// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Providers;

/// <summary>
/// Defines methods for retrieving available data types for a database provider.
/// </summary>
public interface IProviderDataTypeRegistry
{
    /// <summary>
    /// Gets all available data types for the provider.
    /// </summary>
    /// <param name="includeAdvanced">If true, includes advanced/specialized types; otherwise, only common types.</param>
    /// <returns>A collection of available data types.</returns>
    IEnumerable<DataTypeInfo> GetAvailableDataTypes(bool includeAdvanced = false);

    /// <summary>
    /// Gets a specific data type by name.
    /// </summary>
    /// <param name="typeName">The name of the data type to retrieve.</param>
    /// <returns>The data type information, or null if not found.</returns>
    DataTypeInfo? GetDataTypeByName(string typeName);

    /// <summary>
    /// Gets all data types for a specific category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>A collection of data types in the specified category.</returns>
    IEnumerable<DataTypeInfo> GetDataTypesForCategory(DataTypeCategory category);

    /// <summary>
    /// Gets the categories that have data types.
    /// </summary>
    /// <returns>A collection of categories that contain data types.</returns>
    IEnumerable<DataTypeCategory> GetAvailableCategories();
}
