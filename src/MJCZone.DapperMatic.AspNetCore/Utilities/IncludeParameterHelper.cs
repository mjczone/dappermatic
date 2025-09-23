// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Utilities;

/// <summary>
/// Helper class for parsing and handling include query parameters.
/// </summary>
public static class IncludeParameterHelper
{
    private const string WildcardToken = "*";
    private const char Separator = ',';

    /// <summary>
    /// Parses the include parameter string into a set of included fields.
    /// </summary>
    /// <param name="includeParameter">The include parameter value from the query string.</param>
    /// <returns>A set of included field names, or null if the parameter is empty.</returns>
    public static HashSet<string>? ParseIncludeParameter(string? includeParameter)
    {
        if (string.IsNullOrWhiteSpace(includeParameter))
        {
            return null;
        }

        var includes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parts = includeParameter.Split(
            Separator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

        foreach (var part in parts)
        {
            if (!string.IsNullOrWhiteSpace(part))
            {
                includes.Add(part);
            }
        }

        return includes.Count > 0 ? includes : null;
    }

    /// <summary>
    /// Determines if a specific field should be included based on the include parameter.
    /// </summary>
    /// <param name="includes">The parsed include set.</param>
    /// <param name="fieldName">The name of the field to check.</param>
    /// <returns>True if the field should be included, false otherwise.</returns>
    public static bool ShouldInclude(HashSet<string>? includes, string fieldName)
    {
        if (includes == null)
        {
            // No include parameter means return minimal response (exclude by default)
            return false;
        }

        // Check for wildcard
        if (includes.Contains(WildcardToken))
        {
            return true;
        }

        // Check for specific field
        return includes.Contains(fieldName);
    }

    /// <summary>
    /// Determines if the include parameter contains the wildcard token.
    /// </summary>
    /// <param name="includes">The parsed include set.</param>
    /// <returns>True if wildcard is present, false otherwise.</returns>
    public static bool IsWildcard(HashSet<string>? includes)
    {
        return includes?.Contains(WildcardToken) == true;
    }
}
