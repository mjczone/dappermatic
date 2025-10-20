// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Providers.Base;

namespace MJCZone.DapperMatic.Providers.PostgreSql;

/// <summary>
/// Provides PostgreSQL specific database methods.
/// </summary>
public partial class PostgreSqlMethods : DatabaseMethodsBase<PostgreSqlProviderTypeMap>, IPostgreSqlMethods
{
    private static string _defaultSchema = "public";

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlMethods"/> class.
    /// </summary>
    internal PostgreSqlMethods()
        : base(DbProviderType.PostgreSql) { }

    /// <summary>
    /// Gets the characters used for quoting identifiers.
    /// </summary>
    public override char[] QuoteChars => ['"'];

    /// <summary>
    /// Gets the default schema.
    /// </summary>
    protected override string DefaultSchema => _defaultSchema;

    /// <summary>
    /// Sets the default schema.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    public static void SetDefaultSchema(string schema)
    {
        _defaultSchema = schema;
    }

    /// <summary>
    /// Determines whether the database supports ordered keys in constraints.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean value.</returns>
    public override Task<bool> SupportsOrderedKeysInConstraintsAsync(
        IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the database version.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the database version.</returns>
    public override async Task<Version> GetDatabaseVersionAsync(
        IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        // sample output: PostgreSQL 15.7 (Debian 15.7-1.pgdg110+1) on x86_64-pc-linux-gnu, compiled by gcc (Debian 10.2.1-6) 10.2.1 20210110, 64-bit
        const string sql = "SELECT VERSION()";
        var versionString =
            await ExecuteScalarAsync<string>(db, sql, tx: tx, cancellationToken: cancellationToken)
                .ConfigureAwait(false) ?? string.Empty;
        return DbProviderUtils.ExtractVersionFromVersionString(versionString);
    }

    /// <summary>
    /// Discovers custom data types in the PostgreSQL database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of custom data type information.</returns>
    public override async Task<IEnumerable<Models.DataTypeInfo>> DiscoverCustomDataTypesAsync(
        IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var customTypes = new List<Models.DataTypeInfo>();

        // Discover domains (user-defined types based on existing types)
        var domainsSql =
            @"
            SELECT
                domain_name,
                data_type,
                character_maximum_length,
                numeric_precision,
                numeric_scale
            FROM information_schema.domains
            WHERE domain_schema NOT IN ('pg_catalog', 'information_schema')
            ORDER BY domain_name";

        var domains = await QueryAsync<dynamic>(db, domainsSql, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        foreach (var domain in domains)
        {
            customTypes.Add(
                new Models.DataTypeInfo
                {
                    DataType = (string)domain.domain_name,
                    Category = Models.DataTypeCategory.Custom,
                    IsCustom = true,
                    IsCommon = false,
                    Description = $"Domain based on {domain.data_type}",
                    SupportsLength = domain.character_maximum_length != null,
                    MaxLength = domain.character_maximum_length,
                    SupportsPrecision = domain.numeric_precision != null,
                    MaxPrecision = domain.numeric_precision,
                    SupportsScale = domain.numeric_scale != null,
                    MaxScale = domain.numeric_scale,
                }
            );
        }

        // Discover enums
        var enumsSql =
            @"
            SELECT
                t.typname as enum_name,
                array_agg(e.enumlabel ORDER BY e.enumsortorder) as enum_values
            FROM pg_type t
            JOIN pg_enum e ON t.oid = e.enumtypid
            WHERE t.typtype = 'e'
            GROUP BY t.typname
            ORDER BY t.typname";

        var enums = await QueryAsync<dynamic>(db, enumsSql, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        foreach (var enumType in enums)
        {
            var enumValues = (string[])enumType.enum_values;
            customTypes.Add(
                new Models.DataTypeInfo
                {
                    DataType = (string)enumType.enum_name,
                    Category = Models.DataTypeCategory.Custom,
                    IsCustom = true,
                    IsCommon = false,
                    Description = $"Enum with values: {string.Join(", ", enumValues)}",
                    Examples = [.. enumValues],
                }
            );
        }

        // Discover composite types
        var compositesSql =
            @"
            SELECT
                t.typname as type_name,
                array_agg(a.attname ORDER BY a.attnum) as column_names,
                array_agg(pg_catalog.format_type(a.atttypid, a.atttypmod) ORDER BY a.attnum) as column_types
            FROM pg_type t
            JOIN pg_attribute a ON t.oid = a.attrelid
            WHERE t.typtype = 'c'
              AND a.attnum > 0
              AND NOT a.attisdropped
            GROUP BY t.typname
            ORDER BY t.typname";

        var composites = await QueryAsync<dynamic>(db, compositesSql, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        foreach (var composite in composites)
        {
            var columnNames = (string[])composite.column_names;
            var columnTypes = (string[])composite.column_types;
            var columns = columnNames.Zip(columnTypes, (name, type) => $"{name}: {type}");

            customTypes.Add(
                new Models.DataTypeInfo
                {
                    DataType = (string)composite.type_name,
                    Category = Models.DataTypeCategory.Custom,
                    IsCustom = true,
                    IsCommon = false,
                    Description = $"Composite type with columns: {string.Join(", ", columns)}",
                }
            );
        }

        return customTypes;
    }

    /// <summary>
    /// Normalizes the name to lowercase.
    /// </summary>
    /// <param name="name">The name to normalize.</param>
    /// <returns>The normalized name.</returns>
    protected override string NormalizeName(string name)
    {
        return base.NormalizeName(name).ToLowerInvariant();
    }

    /// <summary>
    /// Converts the text to a LIKE string, normalizing to lowercase.
    /// </summary>
    /// <param name="text">The text to convert.</param>
    /// <param name="allowedSpecialChars">The allowed special characters.</param>
    /// <returns>The LIKE string.</returns>
    protected override string ToLikeString(string text, string allowedSpecialChars = "-_.*")
    {
        return base.ToLikeString(text, allowedSpecialChars).ToLowerInvariant();
    }

    /// <inheritdoc />
    protected override IProviderDataTypeRegistry GetDataTypeRegistry()
    {
        return new PostgreSqlDataTypeRegistry();
    }
}
