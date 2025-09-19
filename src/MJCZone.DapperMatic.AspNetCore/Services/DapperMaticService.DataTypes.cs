// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Security.Claims;
using System.Text;
using Dapper;
using MJCZone.DapperMatic.AspNetCore.Extensions;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Models.Requests;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Providers;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Partial class containing view-related methods for DapperMaticService.
/// </summary>
public sealed partial class DapperMaticService
{
    /// <summary>
    /// Retrieves the list of data types supported by the specified datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="includeCustomTypes">If true, discovers custom types from the database in addition to static types.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of data types available in the datasource, including provider-specific types, extensions, and custom types.</returns>
    public async Task<(
        string providerName,
        List<DataTypeInfo> dataTypes
    )> GetDatasourceDataTypesAsync(
        string datasourceId,
        ClaimsPrincipal? user = null,
        bool includeCustomTypes = false,
        CancellationToken cancellationToken = default
    )
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasourceId));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.ListDataTypes,
            DatasourceId = datasourceId,
        };

        try
        {
            // Check permissions
            if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
            {
                await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
                throw new UnauthorizedAccessException(
                    $"Access denied to datasource '{datasourceId}'"
                );
            }

            // Get datasource to verify it exists and get the provider type
            var datasource = await _datasourceRepository
                .GetDatasourceAsync(datasourceId)
                .ConfigureAwait(false);

            if (datasource == null)
            {
                await LogAuditEventAsync(context, false, "Datasource not found")
                    .ConfigureAwait(false);
                throw new ArgumentException($"Datasource '{datasourceId}' not found");
            }

            var providerName = datasource.Provider ?? "Unknown";

            // Create connection to get database methods
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);
            using (connection)
            {
                var databaseMethods = DatabaseMethodsProvider.GetMethods(connection);
                var staticTypes = databaseMethods
                    .GetAvailableDataTypes(includeAdvanced: true)
                    .ToList();

                // If custom types are requested, discover them from the database
                if (includeCustomTypes)
                {
                    // Open the connection if not already open
                    if (connection.State != ConnectionState.Open)
                    {
                        connection.Open();
                    }

                    try
                    {
                        var customTypes = await databaseMethods
                            .DiscoverCustomDataTypesAsync(
                                connection,
                                cancellationToken: cancellationToken
                            )
                            .ConfigureAwait(false);

                        staticTypes.AddRange(customTypes);
                    }
                    catch (Exception ex)
                    {
                        // Log custom type discovery errors but don't fail the entire operation
                        await LogAuditEventAsync(
                                context,
                                true,
                                $"Warning: Custom type discovery failed: {ex.Message}"
                            )
                            .ConfigureAwait(false);
                    }
                }

                await LogAuditEventAsync(context, true).ConfigureAwait(false);

                return (providerName, staticTypes);
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not ArgumentException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }
}
