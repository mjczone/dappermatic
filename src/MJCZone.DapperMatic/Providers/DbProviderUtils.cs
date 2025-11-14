// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.RegularExpressions;

namespace MJCZone.DapperMatic.Providers;

/// <summary>
/// Utility class for generating database constraint and index names.
/// </summary>
public static partial class DbProviderUtils
{
    /// <summary>
    /// Generates a check constraint name.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="columnName">The name of the column.</param>
    /// <returns>The generated check constraint name.</returns>
    public static string GenerateCheckConstraintName(string tableName, string columnName)
    {
        return "ck".ToRawIdentifier(tableName, columnName);
    }

    /// <summary>
    /// Generates a default constraint name.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="columnName">The name of the column.</param>
    /// <returns>The generated default constraint name.</returns>
    public static string GenerateDefaultConstraintName(string tableName, string columnName)
    {
        return "df".ToRawIdentifier(tableName, columnName);
    }

    /// <summary>
    /// Generates a unique constraint name.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="columnNames">The names of the columns.</param>
    /// <returns>The generated unique constraint name.</returns>
    public static string GenerateUniqueConstraintName(string tableName, params string[] columnNames)
    {
        return "uc".ToRawIdentifier([tableName, .. columnNames]);
    }

    /// <summary>
    /// Generates a primary key constraint name.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="columnNames">The names of the columns.</param>
    /// <returns>The generated primary key constraint name.</returns>
    public static string GeneratePrimaryKeyConstraintName(string tableName, params string[] columnNames)
    {
        return "pk".ToRawIdentifier([tableName, .. columnNames]);
    }

    /// <summary>
    /// Generates an index name.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="columnNames">The names of the columns.</param>
    /// <returns>The generated index name.</returns>
    public static string GenerateIndexName(string tableName, params string[] columnNames)
    {
        return "ix".ToRawIdentifier([tableName, .. columnNames]);
    }

    /// <summary>
    /// Generates a foreign key constraint name.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="columnName">The name of the column.</param>
    /// <param name="refTableName">The name of the referenced table.</param>
    /// <param name="refColumnName">The name of the referenced column.</param>
    /// <returns>The generated foreign key constraint name.</returns>
    public static string GenerateForeignKeyConstraintName(
        string tableName,
        string columnName,
        string refTableName,
        string refColumnName
    )
    {
        return "fk".ToRawIdentifier(tableName, columnName, refTableName, refColumnName);
    }

    /// <summary>
    /// Generates a foreign key constraint name.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="columnNames">The names of the columns.</param>
    /// <param name="refTableName">The name of the referenced table.</param>
    /// <param name="refColumnNames">The names of the referenced columns.</param>
    /// <returns>The generated foreign key constraint name.</returns>
    public static string GenerateForeignKeyConstraintName(
        string tableName,
        string[] columnNames,
        string refTableName,
        string[] refColumnNames
    )
    {
        return "fk".ToRawIdentifier([tableName, .. columnNames, refTableName, .. refColumnNames]);
    }

    [GeneratedRegex(@"\d+(\.\d+)+")]
    private static partial Regex VersionPatternRegex();

#pragma warning disable SA1201 // Elements should appear in the correct order
    private static readonly Regex VersionPattern = VersionPatternRegex();
#pragma warning restore SA1201 // Elements should appear in the correct order

    /// <summary>
    /// Extracts the version from a version string.
    /// </summary>
    /// <param name="versionString">The version string.</param>
    /// <returns>The extracted version.</returns>
    /// <exception cref="ArgumentException">Thrown when the version cannot be extracted.</exception>
    internal static Version ExtractVersionFromVersionString(string versionString)
    {
        var m = VersionPattern.Match(versionString);
        var version = m.Value;
        return Version.TryParse(version, out var vs)
            ? vs
            : throw new ArgumentException($"Could not extract version from: {versionString}", nameof(versionString));
    }
}
