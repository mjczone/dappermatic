// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Validation;

namespace MJCZone.DapperMatic.AspNetCore.Services;

public partial class DapperMaticService
{
    /// <inheritdoc />
    public async Task<IEnumerable<DatasourceDto>> GetDatasourcesAsync(
        IOperationContext context,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        Validate.Arguments().NotNull(context, nameof(context)).Assert();

        var result = await _datasourceRepository.GetDatasourcesAsync().ConfigureAwait(false);
        await LogAuditEventAsync(context, true, $"Retrieved all datasources").ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public async Task<DatasourceDto> GetDatasourceAsync(
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

        var datasource = await _datasourceRepository
            .GetDatasourceAsync(datasourceId)
            .ConfigureAwait(false);

        if (datasource == null)
        {
            throw new KeyNotFoundException($"Datasource '{datasourceId}' not found");
        }

        await LogAuditEventAsync(context, true, $"Retrieved datasource '{datasourceId}'")
            .ConfigureAwait(false);
        return datasource;
    }

    /// <inheritdoc />
    public async Task<DatasourceDto> AddDatasourceAsync(
        IOperationContext context,
        DatasourceDto datasource,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        Validate
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNull(datasource, nameof(datasource))
            .Assert();

        var created = await _datasourceRepository
            .AddDatasourceAsync(datasource)
            .ConfigureAwait(false);

        if (!created)
        {
            throw new InvalidOperationException("Failed to create datasource");
        }

        var newDatasource = await _datasourceRepository
            .GetDatasourceAsync(datasource.Id!)
            .ConfigureAwait(false);

        if (newDatasource == null)
        {
            throw new InvalidOperationException("Failed to retrieve newly created datasource");
        }

        await LogAuditEventAsync(context, true, $"Datasource '{newDatasource.Id}' created")
            .ConfigureAwait(false);
        return newDatasource;
    }

    /// <inheritdoc />
    public async Task<DatasourceDto> UpdateDatasourceAsync(
        IOperationContext context,
        DatasourceDto datasource,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        Validate
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNull(datasource, nameof(datasource))
            .Object(
                datasource,
                nameof(datasource),
                builder => builder.NotNullOrWhiteSpace(d => d.Id, nameof(datasource.Id))
            )
            .Assert();

        var existing = await _datasourceRepository
            .GetDatasourceAsync(datasource.Id!)
            .ConfigureAwait(false);

        if (existing == null)
        {
            throw new KeyNotFoundException($"Datasource '{datasource.Id}' not found");
        }

        var updated = await _datasourceRepository
            .UpdateDatasourceAsync(datasource!)
            .ConfigureAwait(false);

        if (!updated)
        {
            throw new InvalidOperationException("Failed to update datasource");
        }

        existing = await _datasourceRepository
            .GetDatasourceAsync(datasource.Id!)
            .ConfigureAwait(false);

        if (existing == null)
        {
            throw new KeyNotFoundException($"Datasource '{datasource.Id}' not found after update");
        }

        await LogAuditEventAsync(context, true, $"Updated datasource '{datasource.Id}'")
            .ConfigureAwait(false);
        return existing;
    }

    /// <summary>
    /// Retrieves a datasource by its ID.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task RemoveDatasourceAsync(
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

        var existing = await _datasourceRepository
            .GetDatasourceAsync(datasourceId)
            .ConfigureAwait(false);

        if (existing == null)
        {
            throw new KeyNotFoundException($"Datasource '{datasourceId}' not found");
        }

        var deleted = await _datasourceRepository
            .RemoveDatasourceAsync(datasourceId)
            .ConfigureAwait(false);

        if (!deleted)
        {
            throw new InvalidOperationException("Failed to delete datasource");
        }

        await LogAuditEventAsync(context, true, $"Removed datasource '{datasourceId}'")
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DatasourceExistsAsync(
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

        var exists = await _datasourceRepository
            .DatasourceExistsAsync(datasourceId)
            .ConfigureAwait(false);

        await LogAuditEventAsync(context, true, $"Checked existence of datasource '{datasourceId}'")
            .ConfigureAwait(false);
        return exists;
    }

    /// <inheritdoc />
    public async Task<DatasourceConnectivityTestDto> TestDatasourceAsync(
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

        var result = new DatasourceConnectivityTestDto { DatasourceId = datasourceId };
        var startTime = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);
            connection.Open();

            result.Connected = true;
            result.DatabaseName = connection.Database;
            result.Provider = connection.GetDbProviderType().ToString();
            result.ServerVersion =
                $"{await connection.GetDatabaseVersionAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false)}";
            result.ResponseTimeMs = startTime.ElapsedMilliseconds;
            await LogAuditEventAsync(
                    context,
                    true,
                    $"Tested datasource '{datasourceId}' connectivity"
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            result.Connected = false;
            result.ErrorMessage = ex.Message;
            result.ResponseTimeMs = startTime.ElapsedMilliseconds;
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
        }

        return result;
    }
}
