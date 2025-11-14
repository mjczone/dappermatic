// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Reflection;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Providers;

/// <summary>
/// Base class for provider-specific data type registries.
/// </summary>
public abstract class ProviderDataTypeRegistryBase : IProviderDataTypeRegistry
{
    private readonly Dictionary<string, DataTypeInfo> _dataTypes;
    private readonly Dictionary<string, DataTypeInfo> _dataTypesByAlias;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderDataTypeRegistryBase"/> class.
    /// </summary>
    protected ProviderDataTypeRegistryBase()
    {
        _dataTypes = new Dictionary<string, DataTypeInfo>(StringComparer.OrdinalIgnoreCase);
        _dataTypesByAlias = new Dictionary<string, DataTypeInfo>(StringComparer.OrdinalIgnoreCase);
        RegisterDataTypes();
        BuildAliasIndex();
    }

    /// <summary>
    /// Gets all available data types, optionally including advanced types.
    /// </summary>
    /// <param name="includeAdvanced">Whether to include advanced (non-common) types.</param>
    /// <returns>A collection of available data types.</returns>
    public virtual IEnumerable<DataTypeInfo> GetAvailableDataTypes(bool includeAdvanced = false)
    {
        var types = _dataTypes.Values.AsEnumerable();

        if (!includeAdvanced)
        {
            types = types.Where(t => t.IsCommon);
        }

        return types.OrderBy(t => t.Category).ThenBy(t => t.DataType);
    }

    /// <inheritdoc />
    public virtual DataTypeInfo? GetDataTypeByName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        // Check main registry
        if (_dataTypes.TryGetValue(typeName, out var dataType))
        {
            return dataType;
        }

        // Check aliases
        if (_dataTypesByAlias.TryGetValue(typeName, out dataType))
        {
            return dataType;
        }

        return null;
    }

    /// <inheritdoc />
    public virtual IEnumerable<DataTypeInfo> GetDataTypesForCategory(DataTypeCategory category)
    {
        return _dataTypes.Values.Where(t => t.Category == category).OrderBy(t => t.DataType);
    }

    /// <inheritdoc />
    public virtual IEnumerable<DataTypeCategory> GetAvailableCategories()
    {
        return _dataTypes.Values.Select(t => t.Category).Distinct().OrderBy(c => c);
    }

    /// <summary>
    /// Registers the data types for the provider.
    /// </summary>
    protected abstract void RegisterDataTypes();

    /// <summary>
    /// Registers a data type.
    /// </summary>
    /// <param name="dataType">The data type to register.</param>
    protected void RegisterDataType(DataTypeInfo dataType)
    {
        ArgumentNullException.ThrowIfNull(dataType);

        _dataTypes[dataType.DataType] = dataType;
    }

    /// <summary>
    /// Creates a data type with standard string parameters.
    /// </summary>
    /// <param name="typeName">The name of the data type.</param>
    /// <param name="maxLength">The maximum length, if applicable.</param>
    /// <param name="defaultLength">The default length, if applicable.</param>
    /// <param name="isCommon">Whether this is a commonly used type.</param>
    /// <param name="description">An optional description of the data type.</param>
    /// <param name="aliases">Any aliases for this data type.</param>
    /// <returns>A configured <see cref="DataTypeInfo"/> instance.</returns>
    protected static DataTypeInfo CreateStringType(
        string typeName,
        int? maxLength = null,
        int? defaultLength = null,
        bool isCommon = false,
        string? description = null,
        params string[] aliases
    )
    {
        return new DataTypeInfo
        {
            DataType = typeName,
            Category = DataTypeCategory.Text,
            SupportsLength = maxLength.HasValue,
            MinLength = 1,
            MaxLength = maxLength,
            DefaultLength = defaultLength,
            IsCommon = isCommon,
            Description = description,
            Aliases = [.. aliases],
        };
    }

    /// <summary>
    /// Creates a data type with precision and scale parameters.
    /// </summary>
    /// <param name="typeName">The name of the data type.</param>
    /// <param name="maxPrecision">The maximum precision.</param>
    /// <param name="maxScale">The maximum scale.</param>
    /// <param name="defaultPrecision">The default precision.</param>
    /// <param name="defaultScale">The default scale.</param>
    /// <param name="isCommon">Whether this is a commonly used type.</param>
    /// <param name="description">An optional description of the data type.</param>
    /// <param name="aliases">Any aliases for this data type.</param>
    /// <returns>A configured <see cref="DataTypeInfo"/> instance.</returns>
    protected static DataTypeInfo CreateDecimalType(
        string typeName,
        int maxPrecision,
        int maxScale,
        int defaultPrecision = 18,
        int defaultScale = 2,
        bool isCommon = false,
        string? description = null,
        params string[] aliases
    )
    {
        return new DataTypeInfo
        {
            DataType = typeName,
            Category = DataTypeCategory.Decimal,
            SupportsPrecision = true,
            MinPrecision = 1,
            MaxPrecision = maxPrecision,
            DefaultPrecision = defaultPrecision,
            SupportsScale = true,
            MinScale = 0,
            MaxScale = maxScale,
            DefaultScale = defaultScale,
            IsCommon = isCommon,
            Description = description,
            Aliases = [.. aliases],
        };
    }

    /// <summary>
    /// Creates a simple data type without parameters.
    /// </summary>
    /// <param name="typeName">The name of the data type.</param>
    /// <param name="category">The category of the data type.</param>
    /// <param name="isCommon">Whether this is a commonly used type.</param>
    /// <param name="description">An optional description of the data type.</param>
    /// <param name="aliases">Any aliases for this data type.</param>
    /// <returns>A configured <see cref="DataTypeInfo"/> instance.</returns>
    protected static DataTypeInfo CreateSimpleType(
        string typeName,
        DataTypeCategory category,
        bool isCommon = false,
        string? description = null,
        params string[] aliases
    )
    {
        return new DataTypeInfo
        {
            DataType = typeName,
            Category = category,
            IsCommon = isCommon,
            Description = description,
            Aliases = [.. aliases],
        };
    }

    /// <summary>
    /// Creates an integer data type.
    /// </summary>
    /// <param name="typeName">The name of the data type.</param>
    /// <param name="description">An optional description of the data type.</param>
    /// <param name="isCommon">Whether this is a commonly used type.</param>
    /// <param name="aliases">Any aliases for this data type.</param>
    /// <returns>A configured <see cref="DataTypeInfo"/> instance.</returns>
    protected static DataTypeInfo CreateIntegerType(
        string typeName,
        string? description = null,
        bool isCommon = false,
        params string[] aliases
    )
    {
        return CreateSimpleType(typeName, DataTypeCategory.Integer, isCommon, description, aliases);
    }

    /// <summary>
    /// Creates a datetime type with optional precision.
    /// </summary>
    /// <param name="typeName">The name of the data type.</param>
    /// <param name="supportsPrecision">Whether the type supports precision.</param>
    /// <param name="maxPrecision">The maximum precision, if supported.</param>
    /// <param name="defaultPrecision">The default precision, if supported.</param>
    /// <param name="isCommon">Whether this is a commonly used type.</param>
    /// <param name="description">An optional description of the data type.</param>
    /// <param name="aliases">Any aliases for this data type.</param>
    /// <returns>A configured <see cref="DataTypeInfo"/> instance.</returns>
    protected static DataTypeInfo CreateDateTimeType(
        string typeName,
        bool supportsPrecision = false,
        int? maxPrecision = null,
        int? defaultPrecision = null,
        bool isCommon = false,
        string? description = null,
        params string[] aliases
    )
    {
        return new DataTypeInfo
        {
            DataType = typeName,
            Category = DataTypeCategory.DateTime,
            SupportsPrecision = supportsPrecision,
            MinPrecision = supportsPrecision ? 0 : null,
            MaxPrecision = supportsPrecision ? maxPrecision : null,
            DefaultPrecision = supportsPrecision ? defaultPrecision : null,
            IsCommon = isCommon,
            Description = description,
            Aliases = [.. aliases],
        };
    }

    /// <summary>
    /// Creates a binary data type.
    /// </summary>
    /// <param name="typeName">The name of the data type.</param>
    /// <param name="maxLength">The maximum length, if applicable.</param>
    /// <param name="defaultLength">The default length, if applicable.</param>
    /// <param name="isCommon">Whether this is a commonly used type.</param>
    /// <param name="description">An optional description of the data type.</param>
    /// <param name="aliases">Any aliases for this data type.</param>
    /// <returns>A configured <see cref="DataTypeInfo"/> instance.</returns>
    protected static DataTypeInfo CreateBinaryType(
        string typeName,
        int? maxLength = null,
        int? defaultLength = null,
        bool isCommon = false,
        string? description = null,
        params string[] aliases
    )
    {
        return new DataTypeInfo
        {
            DataType = typeName,
            Category = DataTypeCategory.Binary,
            SupportsLength = maxLength.HasValue,
            MinLength = maxLength.HasValue ? 1 : null,
            MaxLength = maxLength,
            DefaultLength = defaultLength,
            IsCommon = isCommon,
            Description = description,
            Aliases = [.. aliases],
        };
    }

    /// <summary>
    /// Discovers and registers data types from a static types class using reflection.
    /// </summary>
    /// <typeparam name="TTypesClass">The static class containing type constants.</typeparam>
    /// <param name="categoryMapper">Function to map type names to categories.</param>
    /// <param name="commonTypes">Set of type names that should be marked as common.</param>
    protected void RegisterTypesFromConstants<TTypesClass>(
        Func<string, string, DataTypeCategory> categoryMapper,
        HashSet<string>? commonTypes = null
    )
    {
        var fields = typeof(TTypesClass)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string));

        foreach (var field in fields)
        {
            var fieldName = field.Name;
            if (fieldName.StartsWith("sql_", StringComparison.OrdinalIgnoreCase))
            {
                var typeName = field.GetValue(null)?.ToString();
                if (!string.IsNullOrWhiteSpace(typeName))
                {
                    var category = categoryMapper(fieldName, typeName);
                    var isCommon = commonTypes?.Contains(typeName) ?? false;

                    RegisterDataType(
                        new DataTypeInfo
                        {
                            DataType = typeName,
                            Category = category,
                            IsCommon = isCommon,
                        }
                    );
                }
            }
        }
    }

    /// <summary>
    /// Builds an index of data types by their aliases.
    /// </summary>
    private void BuildAliasIndex()
    {
        foreach (var dataType in _dataTypes.Values)
        {
            if (dataType.Aliases != null)
            {
                foreach (var alias in dataType.Aliases)
                {
                    _dataTypesByAlias[alias] = dataType;
                }
            }
        }
    }
}
