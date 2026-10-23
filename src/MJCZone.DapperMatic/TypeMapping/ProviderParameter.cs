// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Data;
using System.Reflection;

namespace MJCZone.DapperMatic.TypeMapping;

/// <summary>
/// Tells type handlers which driver a parameter belongs to, from the parameter's own type, without referencing
/// any driver.
/// </summary>
internal static class ProviderParameter
{
    private static readonly ConcurrentDictionary<(Type, string), Action<IDbDataParameter>?> NpgsqlDbTypeSetters =
        new();

    /// <summary>
    /// Determines whether a parameter belongs to Npgsql.
    /// </summary>
    /// <param name="parameter">The parameter.</param>
    /// <returns>True for an Npgsql parameter.</returns>
    public static bool IsNpgsql(IDbDataParameter parameter) => TypeName(parameter).Contains("Npgsql", StringComparison.Ordinal);

    /// <summary>
    /// Determines whether a parameter belongs to MySql.Data or MySqlConnector.
    /// </summary>
    /// <param name="parameter">The parameter.</param>
    /// <returns>True for a MySQL parameter.</returns>
    public static bool IsMySql(IDbDataParameter parameter) => TypeName(parameter).Contains("MySql", StringComparison.Ordinal);

    /// <summary>
    /// Determines whether a parameter belongs to System.Data.SQLite or Microsoft.Data.Sqlite.
    /// </summary>
    /// <param name="parameter">The parameter.</param>
    /// <returns>True for a SQLite parameter.</returns>
    public static bool IsSqlite(IDbDataParameter parameter) =>
        TypeName(parameter).Contains("SQLite", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Sets an Npgsql parameter's <c>NpgsqlDbType</c>, such as <c>Jsonb</c>, found by reflection.
    /// </summary>
    /// <param name="parameter">An Npgsql parameter.</param>
    /// <param name="npgsqlDbType">The name of the <c>NpgsqlDbType</c> value.</param>
    public static void SetNpgsqlDbType(IDbDataParameter parameter, string npgsqlDbType)
    {
        var setter = NpgsqlDbTypeSetters.GetOrAdd(
            (parameter.GetType(), npgsqlDbType),
            key =>
            {
                var property = key.Item1.GetProperty("NpgsqlDbType", BindingFlags.Public | BindingFlags.Instance);
                if (property is null || !property.PropertyType.IsEnum)
                {
                    return null;
                }

                var value = Enum.Parse(property.PropertyType, key.Item2);
                return p => property.SetValue(p, value);
            }
        );

        setter?.Invoke(parameter);
    }

    private static string TypeName(IDbDataParameter parameter) => parameter.GetType().FullName ?? string.Empty;
}
