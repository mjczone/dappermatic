// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Validation;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Partial class containing schema-related methods for DapperMaticService.
/// </summary>
public partial class DapperMaticService
{
    /// <inheritdoc />
    public async Task<IEnumerable<SchemaDto>> GetSchemasAsync(
        IOperationContext context,
        string datasourceId,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        Validate
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .Assert();

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check if provider supports schemas
            if (!connection.SupportsSchemas())
            {
                await LogAuditEventAsync(
                        context,
                        true,
                        $"Attempted to retrieve schemas for datasource '{datasourceId}', but the provider does not support schemas."
                    )
                    .ConfigureAwait(false);
                return [];
            }

            // Get schema names using extension method
            var schemaNames = await connection
                .GetSchemaNamesAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            // Convert to SchemaDto objects
            var schemas = schemaNames.Select(name => new SchemaDto { SchemaName = name });

            await LogAuditEventAsync(
                    context,
                    true,
                    $"Retrieved schemas for datasource '{datasourceId}'"
                )
                .ConfigureAwait(false);
            return schemas;
        }
    }

    /// <inheritdoc />
    public async Task<SchemaDto> GetSchemaAsync(
        IOperationContext context,
        string datasourceId,
        string schemaName,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        var normalizedSchemaName = NormalizeSchemaName(schemaName);

        Validate
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNullOrWhiteSpace(normalizedSchemaName, nameof(schemaName))
            .Assert();

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check if provider supports schemas
            if (!connection.SupportsSchemas())
            {
                throw new InvalidOperationException(
                    $"The provider does not support schema operations."
                );
            }

            // Check if schema exists using extension method
            var exists = await connection
                .DoesSchemaExistAsync(normalizedSchemaName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!exists)
            {
                throw new KeyNotFoundException(
                    $"Schema '{normalizedSchemaName}' does not exist in datasource '{datasourceId}'."
                );
            }

            await LogAuditEventAsync(
                    context,
                    true,
                    $"Retrieved schema '{normalizedSchemaName}' for datasource '{datasourceId}'"
                )
                .ConfigureAwait(false);
            return new SchemaDto { SchemaName = normalizedSchemaName };
        }
    }

    /// <inheritdoc />
    public async Task<SchemaDto> CreateSchemaAsync(
        IOperationContext context,
        string datasourceId,
        SchemaDto schema,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        var normalizedSchemaName = NormalizeSchemaName(schema.SchemaName);

        Validate
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNull(schema, nameof(schema))
            .NotNullOrWhiteSpace(normalizedSchemaName, nameof(schema.SchemaName))
            .Assert();

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check if provider supports schemas
            if (!connection.SupportsSchemas())
            {
                throw new InvalidOperationException(
                    $"The provider does not support schema operations."
                );
            }

            // Check schema does not already exist
            await AssertSchemaDoesNotExistAsync(
                    datasourceId,
                    normalizedSchemaName,
                    connection,
                    cancellationToken
                )
                .ConfigureAwait(false);

            // Create schema using extension method
            var created = await connection
                .CreateSchemaIfNotExistsAsync(
                    normalizedSchemaName,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);

            if (!created)
            {
                throw new InvalidOperationException(
                    $"Failed to create schema '{normalizedSchemaName}' for an unknown reason."
                );
            }

            await LogAuditEventAsync(context, true, $"Created schema '{normalizedSchemaName}'")
                .ConfigureAwait(false);
            return new SchemaDto { SchemaName = normalizedSchemaName };
        }
    }

    /// <inheritdoc />
    public async Task DropSchemaAsync(
        IOperationContext context,
        string datasourceId,
        string schemaName,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        var normalizedSchemaName = NormalizeSchemaName(schemaName);

        Validate
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNullOrWhiteSpace(normalizedSchemaName, nameof(schemaName))
            .Assert();

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check if provider supports schemas
            if (!connection.SupportsSchemas())
            {
                throw new InvalidOperationException(
                    $"The provider does not support schema operations."
                );
            }

            // Check schema exists
            await AssertSchemaExistsIfSpecifiedAsync(
                    datasourceId,
                    normalizedSchemaName,
                    connection,
                    cancellationToken
                )
                .ConfigureAwait(false);

            // Drop schema using extension method
            var dropped = await connection
                .DropSchemaIfExistsAsync(normalizedSchemaName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!dropped)
            {
                throw new InvalidOperationException(
                    $"Failed to drop schema '{normalizedSchemaName}' for an unknown reason."
                );
            }

            await LogAuditEventAsync(context, true, $"Dropped schema '{normalizedSchemaName}'")
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<bool> SchemaExistsAsync(
        IOperationContext context,
        string datasourceId,
        string schemaName,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        var normalizedSchemaName = NormalizeSchemaName(schemaName);

        Validate
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNullOrWhiteSpace(normalizedSchemaName, nameof(schemaName))
            .Assert();

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check if provider supports schemas
            if (!connection.SupportsSchemas())
            {
                throw new InvalidOperationException(
                    $"The provider does not support schema operations."
                );
            }

            // Check if schema exists using extension method
            var exists = await connection
                .DoesSchemaExistAsync(normalizedSchemaName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await LogAuditEventAsync(
                    context,
                    true,
                    exists == true
                        ? $"Schema '{normalizedSchemaName}' exists."
                        : $"Schema '{normalizedSchemaName}' does not exist."
                )
                .ConfigureAwait(false);
            return exists;
        }
    }
}
