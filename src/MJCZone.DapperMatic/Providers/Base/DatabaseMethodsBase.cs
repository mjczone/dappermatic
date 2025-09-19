// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using Dapper;
using MJCZone.DapperMatic.Interfaces;

namespace MJCZone.DapperMatic.Providers.Base;

/// <inheritdoc />
public abstract class DatabaseMethodsBase<TMap> : DatabaseMethodsBase
    where TMap : IDbProviderTypeMap, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseMethodsBase{TMap}"/> class.
    /// </summary>
    /// <param name="providerType">The database provider type.</param>
    internal DatabaseMethodsBase(DbProviderType providerType)
    {
        ProviderType = providerType;
    }

    /// <inheritdoc />
    public override DbProviderType ProviderType { get; }

    /// <inheritdoc />
    public override IDbProviderTypeMap ProviderTypeMap => new TMap();

    /// <inheritdoc />
    protected override async Task<List<TOutput>> QueryAsync<TOutput>(
        IDbConnection db,
        string sql,
        object? param = null,
        IDbTransaction? tx = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default
    )
    {
        return (
            await db.QueryAsync<TOutput>(sql, param, tx, commandTimeout, commandType)
                .ConfigureAwait(false)
        ).AsList();
    }

    /// <inheritdoc />
    protected override async Task<TOutput?> ExecuteScalarAsync<TOutput>(
        IDbConnection db,
        string sql,
        object? param = null,
        IDbTransaction? tx = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default
    )
        where TOutput : default
    {
        var result = await db.ExecuteScalarAsync<TOutput>(
                sql,
                param,
                tx,
                commandTimeout,
                commandType
            )
            .ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(
        IDbConnection db,
        string sql,
        object? param = null,
        IDbTransaction? tx = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default
    )
    {
        return await db.ExecuteAsync(sql, param, tx, commandTimeout, commandType)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// Represents the base class for database methods.
/// </summary>
public abstract partial class DatabaseMethodsBase : IDatabaseMethods
{
    /// <summary>
    /// Gets the database provider type.
    /// </summary>
    public abstract DbProviderType ProviderType { get; }

    /// <summary>
    /// Gets the provider type map.
    /// </summary>
    public abstract IDbProviderTypeMap ProviderTypeMap { get; }

    /// <summary>
    /// Gets a value indicating whether the provider supports schemas.
    /// </summary>
    public virtual bool SupportsSchemas => !string.IsNullOrWhiteSpace(DefaultSchema);

    /// <summary>
    /// Gets the characters used for quoting identifiers.
    /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays
    public abstract char[] QuoteChars { get; }
#pragma warning restore CA1819 // Properties should not return arrays

    /// <summary>
    /// Gets the default schema.
    /// </summary>
    protected abstract string DefaultSchema { get; }

    /// <summary>
    /// Determines whether the provider supports check constraints.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean value indicating whether the provider supports check constraints.</returns>
    public virtual Task<bool> SupportsCheckConstraintsAsync(
        IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(true);

    /// <summary>
    /// Determines whether the provider supports ordered keys in constraints.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean value indicating whether the provider supports ordered keys in constraints.</returns>
    public virtual Task<bool> SupportsOrderedKeysInConstraintsAsync(
        IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(true);

    /// <summary>
    /// Determines whether the provider supports default constraints.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean value indicating whether the provider supports default constraints.</returns>
    public virtual Task<bool> SupportsDefaultConstraintsAsync(
        IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(true);

    /// <summary>
    /// Gets the .NET type descriptor from the SQL type.
    /// </summary>
    /// <param name="sqlType">The SQL type.</param>
    /// <returns>The .NET type descriptor.</returns>
    /// <exception cref="NotSupportedException">Thrown when the SQL type is not supported.</exception>
    public virtual DotnetTypeDescriptor GetDotnetTypeFromSqlType(string sqlType)
    {
        if (
            ProviderTypeMap.TryGetDotnetTypeDescriptorMatchingFullSqlTypeName(
                sqlType,
                out var dotnetTypeDescriptor
            )
            && dotnetTypeDescriptor?.DotnetType != null
        )
        {
            return dotnetTypeDescriptor;
        }

        throw new NotSupportedException($"SQL type {sqlType} is not supported.");
    }

    /// <summary>
    /// Gets the SQL type from the .NET type descriptor.
    /// </summary>
    /// <param name="descriptor">The .NET type descriptor.</param>
    /// <returns>The SQL type.</returns>
    /// <exception cref="NotSupportedException">Thrown when no provider data type is found for the .NET type.</exception>
    public string GetSqlTypeFromDotnetType(DotnetTypeDescriptor descriptor)
    {
        if (
            ProviderTypeMap.TryGetProviderSqlTypeMatchingDotnetType(
                descriptor,
                out var providerDataType
            ) && !string.IsNullOrWhiteSpace(providerDataType?.SqlTypeName)
        )
        {
            return providerDataType.SqlTypeName;
        }

        throw new NotSupportedException($"No provider data type found for .NET type {descriptor}.");
    }

    /// <summary>
    /// Gets the database version asynchronously.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the database version.</returns>
    public abstract Task<Version> GetDatabaseVersionAsync(
        IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the schema-qualified identifier name.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <returns>The schema-qualified identifier name.</returns>
    public virtual string GetSchemaQualifiedIdentifierName(string? schemaName, string tableName)
    {
        schemaName = NormalizeSchemaName(schemaName);
        tableName = NormalizeName(tableName);

        return SupportsSchemas && !string.IsNullOrWhiteSpace(schemaName)
            ? $"{schemaName.ToQuotedIdentifier(QuoteChars)}.{tableName.ToQuotedIdentifier(QuoteChars)}"
            : tableName.ToQuotedIdentifier(QuoteChars);
    }

    /// <summary>
    /// Gets the available data types for this database provider.
    /// </summary>
    /// <param name="includeAdvanced">If true, includes advanced/specialized types; otherwise, only common types.</param>
    /// <returns>A collection of available data types.</returns>
    public virtual IEnumerable<Models.DataTypeInfo> GetAvailableDataTypes(
        bool includeAdvanced = false
    )
    {
        return GetDataTypeRegistry().GetAvailableDataTypes(includeAdvanced);
    }

    /// <summary>
    /// Discovers custom data types from the database (e.g., user-defined types, domains, enums).
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains custom data types.</returns>
    public virtual Task<IEnumerable<Models.DataTypeInfo>> DiscoverCustomDataTypesAsync(
        IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        // Default implementation returns no custom types
        return Task.FromResult(Enumerable.Empty<Models.DataTypeInfo>());
    }

    /// <summary>
    /// Normalizes the name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The normalized name.</returns>
    protected virtual string NormalizeName(string name)
    {
        return name.ToAlphaNumeric("_");
    }

    /// <summary>
    /// Normalizes the schema name.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <returns>The normalized schema name.</returns>
    protected virtual string? NormalizeSchemaName(string? schemaName)
    {
        if (!SupportsSchemas)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(schemaName) ? DefaultSchema : NormalizeName(schemaName);
    }

    /// <summary>
    /// Normalizes the names.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="identifierName">The identifier name.</param>
    /// <returns>The normalized names.</returns>
    protected virtual (string? schemaName, string tableName, string identifierName) NormalizeNames(
        string? schemaName = null,
        string? tableName = null,
        string? identifierName = null
    )
    {
        schemaName = NormalizeSchemaName(schemaName);

        if (!string.IsNullOrWhiteSpace(tableName))
        {
            tableName = NormalizeName(tableName);
        }

        if (!string.IsNullOrWhiteSpace(identifierName))
        {
            identifierName = NormalizeName(identifierName);
        }

        return (schemaName, tableName ?? string.Empty, identifierName ?? string.Empty);
    }

    /// <summary>
    /// Executes a query asynchronously and returns the result as a list of <typeparamref name="TOutput"/>.
    /// </summary>
    /// <typeparam name="TOutput">The type of the result.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="sql">The SQL query.</param>
    /// <param name="param">The query parameters.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="commandTimeout">The command timeout.</param>
    /// <param name="commandType">The command type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the list of <typeparamref name="TOutput"/>.</returns>
    protected abstract Task<List<TOutput>> QueryAsync<TOutput>(
        IDbConnection db,
        string sql,
        object? param = null,
        IDbTransaction? tx = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Executes a scalar query asynchronously and returns the result.
    /// </summary>
    /// <typeparam name="TOutput">The type of the result.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="sql">The SQL query.</param>
    /// <param name="param">The query parameters.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="commandTimeout">The command timeout.</param>
    /// <param name="commandType">The command type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the result of the query.</returns>
    protected abstract Task<TOutput?> ExecuteScalarAsync<TOutput>(
        IDbConnection db,
        string sql,
        object? param = null,
        IDbTransaction? tx = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Executes a command asynchronously and returns the number of affected rows.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="sql">The SQL command.</param>
    /// <param name="param">The command parameters.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="commandTimeout">The command timeout.</param>
    /// <param name="commandType">The command type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of affected rows.</returns>
    protected abstract Task<int> ExecuteAsync(
        IDbConnection db,
        string sql,
        object? param = null,
        IDbTransaction? tx = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the quoted identifier.
    /// </summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The quoted identifier.</returns>
    protected virtual string GetQuotedIdentifier(string identifier)
    {
        return string.Empty.ToQuotedIdentifier(QuoteChars, identifier);
    }

    /// <summary>
    /// Gets the quoted compound identifier.
    /// </summary>
    /// <param name="identifiers">The identifiers.</param>
    /// <param name="joinCharacter">The union string.</param>
    /// <returns>The quoted compound identifier.</returns>
    protected virtual string GetQuotedCompoundIdentifier(
        string[] identifiers,
        string joinCharacter = "."
    )
    {
        return string.Join(
            joinCharacter,
            identifiers.Select(x => string.Empty.ToQuotedIdentifier(QuoteChars, x))
        );
    }

    /// <summary>
    /// Converts the text to a safe string.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="allowedSpecialChars">The allowed special characters.</param>
    /// <returns>The safe string.</returns>
    protected virtual string ToSafeString(string text, string allowedSpecialChars = "-_.*")
    {
        return text.ToAlphaNumeric(allowedSpecialChars);
    }

    /// <summary>
    /// Converts the text to a LIKE string.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="allowedSpecialChars">The allowed special characters.</param>
    /// <returns>The LIKE string.</returns>
    protected virtual string ToLikeString(string text, string allowedSpecialChars = "-_.*")
    {
        return text.ToAlphaNumeric(allowedSpecialChars)
            .Replace("*", "%", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the data type registry for this provider.
    /// </summary>
    /// <returns>The data type registry.</returns>
    protected abstract IProviderDataTypeRegistry GetDataTypeRegistry();
}
