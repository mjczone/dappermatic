// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.AspNetCore.Validation;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Providers;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Partial class containing view-related methods for DapperMaticService.
/// </summary>
public partial class DapperMaticService
{
    /// <inheritdoc />
    public async Task<(string providerName, List<DataTypeInfo> dataTypes)> GetDatasourceDataTypesAsync(
        IOperationContext context,
        string datasourceId,
        bool includeCustomTypes = false,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        ValidationFactory
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .Assert();

        // Get datasource to verify it exists and get the provider type
        var datasource = await _datasourceRepository.GetDatasourceAsync(datasourceId).ConfigureAwait(false);

        if (datasource == null)
        {
            throw new KeyNotFoundException($"Datasource '{datasourceId}' not found");
        }

        var providerName = datasource.Provider ?? "Unknown";

        // Create connection to get database methods
        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            var databaseMethods = DatabaseMethodsProvider.GetMethods(connection);
            var staticTypes = databaseMethods.GetAvailableDataTypes(includeAdvanced: true).ToList();

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
                        .DiscoverCustomDataTypesAsync(connection, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);

                    staticTypes.AddRange(customTypes);
                }
                catch (Exception ex)
                {
                    // Log custom type discovery errors but don't fail the entire operation
                    await LogAuditEventAsync(context, true, $"Warning: Custom type discovery failed: {ex.Message}")
                        .ConfigureAwait(false);
                }
            }

            await LogAuditEventAsync(context, true, $"Retrieved data types for datasource '{datasourceId}'")
                .ConfigureAwait(false);

            return (providerName, staticTypes);
        }
    }
}
