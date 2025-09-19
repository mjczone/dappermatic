// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Security.Claims;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Security;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Partial class containing schema-related methods for DapperMaticService.
/// </summary>
public sealed partial class DapperMaticService
{
    /// <inheritdoc />
    public async Task<IEnumerable<SchemaDto>> GetSchemasAsync(
        string datasourceId,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasourceId));
        }

        // Create operation context
        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.ListSchemas,
            DatasourceId = datasourceId,
        };

        // Check authorization
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to list schemas for datasource '{datasourceId}'."
            );
        }

        try
        {
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);

            using (connection)
            {
                // Check if provider supports schemas
                if (!connection.SupportsSchemas())
                {
                    // For providers that don't support schemas, return a single "_" schema
                    await LogAuditEventAsync(context, true).ConfigureAwait(false);
                    return new[] { new SchemaDto { SchemaName = "_" } };
                }

                // Get schema names using extension method
                var schemaNames = await connection
                    .GetSchemaNamesAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                // Convert to SchemaDto objects
                var schemas = schemaNames.Select(name => new SchemaDto { SchemaName = name });

                await LogAuditEventAsync(context, true).ConfigureAwait(false);
                return schemas;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SchemaDto?> GetSchemaAsync(
        string datasourceId,
        string schemaName,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasourceId));
        }
        if (string.IsNullOrWhiteSpace(schemaName))
        {
            throw new ArgumentException("Schema name is required.", nameof(schemaName));
        }

        // Create operation context
        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.GetSchema,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
        };

        // Check authorization
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to get schema '{schemaName}' from datasource '{datasourceId}'."
            );
        }

        try
        {
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);

            using (connection)
            {
                // Check if provider supports schemas
                if (!connection.SupportsSchemas())
                {
                    // For providers that don't support schemas, only "_" is valid
                    await LogAuditEventAsync(context, true).ConfigureAwait(false);
                    return schemaName == "_" ? new SchemaDto { SchemaName = "_" } : null;
                }

                // Check if schema exists using extension method
                var exists = await connection
                    .DoesSchemaExistAsync(schemaName, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                var result = exists ? new SchemaDto { SchemaName = schemaName } : null;

                await LogAuditEventAsync(context, true).ConfigureAwait(false);
                return result;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SchemaDto?> CreateSchemaAsync(
        string datasourceId,
        SchemaDto schema,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasourceId));
        }
        ArgumentNullException.ThrowIfNull(schema);
        if (string.IsNullOrWhiteSpace(schema.SchemaName))
        {
            throw new ArgumentException("Schema name is required.", nameof(schema));
        }

        // Create operation context
        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.CreateSchema,
            DatasourceId = datasourceId,
            SchemaName = schema.SchemaName,
            RequestBody = schema,
        };

        // Check authorization
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to create schema '{schema.SchemaName}' in datasource '{datasourceId}'."
            );
        }

        try
        {
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);

            using (connection)
            {
                // Check if provider supports schemas
                if (!connection.SupportsSchemas())
                {
                    await LogAuditEventAsync(context, false, "Provider does not support schemas")
                        .ConfigureAwait(false);
                    throw new NotSupportedException(
                        $"The provider does not support schema operations."
                    );
                }

                // Create schema using extension method
                var created = await connection
                    .CreateSchemaIfNotExistsAsync(
                        schema.SchemaName,
                        cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);

                var result = created ? schema : null;

                await LogAuditEventAsync(context, true).ConfigureAwait(false);
                return result;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DropSchemaAsync(
        string datasourceId,
        string schemaName,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasourceId));
        }
        if (string.IsNullOrWhiteSpace(schemaName))
        {
            throw new ArgumentException("Schema name is required.", nameof(schemaName));
        }

        // Create operation context
        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.DropSchema,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
        };

        // Check authorization
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to drop schema '{schemaName}' from datasource '{datasourceId}'."
            );
        }

        try
        {
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);

            using (connection)
            {
                // Check if provider supports schemas
                if (!connection.SupportsSchemas())
                {
                    await LogAuditEventAsync(context, false, "Provider does not support schemas")
                        .ConfigureAwait(false);
                    throw new NotSupportedException(
                        $"The provider does not support schema operations."
                    );
                }

                // Drop schema using extension method
                var dropped = await connection
                    .DropSchemaIfExistsAsync(schemaName, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                await LogAuditEventAsync(context, true).ConfigureAwait(false);
                return dropped;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SchemaExistsAsync(
        string datasourceId,
        string schemaName,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasourceId));
        }
        if (string.IsNullOrWhiteSpace(schemaName))
        {
            throw new ArgumentException("Schema name is required.", nameof(schemaName));
        }

        // Create operation context
        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.SchemaExists,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
        };

        // Check authorization
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to check if schema '{schemaName}' exists in datasource '{datasourceId}'."
            );
        }

        try
        {
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);

            using (connection)
            {
                // Check if provider supports schemas
                if (!connection.SupportsSchemas())
                {
                    // For providers that don't support schemas, only "_" exists
                    await LogAuditEventAsync(context, true).ConfigureAwait(false);
                    return schemaName == "_";
                }

                // Check if schema exists using extension method
                var exists = await connection
                    .DoesSchemaExistAsync(schemaName, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                await LogAuditEventAsync(context, true).ConfigureAwait(false);
                return exists;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }
}
