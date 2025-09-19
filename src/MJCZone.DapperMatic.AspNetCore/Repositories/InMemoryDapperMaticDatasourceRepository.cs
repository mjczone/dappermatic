// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Repositories;

/// <summary>
/// In-memory implementation of IDapperMaticDatasourceRepository.
/// Provides thread-safe storage of datasource registrations with secure connection string handling.
/// Connection strings are stored but never exposed through public APIs.
/// </summary>
internal sealed class InMemoryDapperMaticDatasourceRepository : DapperMaticDatasourceRepositoryBase
{
    private readonly ConcurrentDictionary<string, DatasourceDto> _datasources = new();
    private readonly IDatasourceIdFactory _datasourceIdFactory;
    private readonly ILogger<InMemoryDapperMaticDatasourceRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryDapperMaticDatasourceRepository"/> class.
    /// </summary>
    /// <param name="options">The DapperMatic options containing the encryption key.</param>
    /// <param name="datasourceIdFactory">The factory to generate datasource IDs.</param>
    /// <param name="logger">The logger instance.</param>
    public InMemoryDapperMaticDatasourceRepository(
        IOptions<DapperMaticOptions> options,
        IDatasourceIdFactory datasourceIdFactory,
        ILogger<InMemoryDapperMaticDatasourceRepository> logger
    )
        : base(options)
    {
        _datasourceIdFactory = datasourceIdFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<bool> AddDatasourceAsync(DatasourceDto datasource)
    {
        ArgumentNullException.ThrowIfNull(datasource);

        if (string.IsNullOrWhiteSpace(datasource.Id))
        {
            datasource.Id = _datasourceIdFactory.GenerateId(datasource);
            if (string.IsNullOrWhiteSpace(datasource.Id))
            {
                throw new ArgumentException("Datasource ID is required.", nameof(datasource));
            }
        }

        if (datasource.Provider == null)
        {
            throw new ArgumentException("Datasource provider is required.", nameof(datasource));
        }

        if (string.IsNullOrWhiteSpace(datasource.DisplayName))
        {
            throw new ArgumentException("Datasource display name is required.", nameof(datasource));
        }

        if (string.IsNullOrWhiteSpace(datasource.ConnectionString))
        {
            throw new ArgumentException(
                "Datasource connection string is required.",
                nameof(datasource)
            );
        }

        if (await DatasourceExistsAsync(datasource.Id).ConfigureAwait(false))
        {
            return false; // Already exists
        }

        // Create a copy to avoid external modification
        DatasourceDto datasourceCopy = new DatasourceDto
        {
            Id = datasource.Id,
            Provider = datasource.Provider,
            ConnectionString = EncryptConnectionString(datasource.ConnectionString),
            DisplayName = datasource.DisplayName,
            Description = datasource.Description,
            Tags = datasource.Tags?.ToList() ?? [],
            IsEnabled = datasource.IsEnabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        return _datasources.TryAdd(datasource.Id.ToLowerInvariant(), datasourceCopy);
    }

    /// <inheritdoc />
    public override Task<bool> UpdateDatasourceAsync(DatasourceDto datasource)
    {
        ArgumentNullException.ThrowIfNull(datasource);

        if (string.IsNullOrWhiteSpace(datasource.Id))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasource));
        }

        // Get the current stored datasource for comparison
        if (
            !_datasources.TryGetValue(
                datasource.Id.ToLowerInvariant(),
                out DatasourceDto? storedDatasource
            )
        )
        {
            return Task.FromResult(false); // Doesn't exist
        }

        // Create updated version based on stored datasource
        DatasourceDto updatedDatasource = new DatasourceDto
        {
            Id = storedDatasource.Id,
            Provider = datasource.Provider ?? storedDatasource.Provider,
            ConnectionString = !string.IsNullOrWhiteSpace(datasource.ConnectionString)
                ? EncryptConnectionString(datasource.ConnectionString)
                : storedDatasource.ConnectionString,
            DisplayName = !string.IsNullOrWhiteSpace(datasource.DisplayName)
                ? datasource.DisplayName
                : storedDatasource.DisplayName,
            Description = !string.IsNullOrWhiteSpace(datasource.Description)
                ? datasource.Description
                : storedDatasource.Description,
            Tags =
                datasource.Tags != null && datasource.Tags.Count != 0
                    ? [.. datasource.Tags]
                    : storedDatasource.Tags?.ToList() ?? [],
            IsEnabled = datasource.IsEnabled ?? storedDatasource.IsEnabled,
            CreatedAt = storedDatasource.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        bool result = _datasources.TryUpdate(
            datasource.Id.ToLowerInvariant(),
            updatedDatasource,
            storedDatasource
        );
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public override Task<bool> RemoveDatasourceAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        bool removed = _datasources.TryRemove(id.ToLowerInvariant(), out _);

        return Task.FromResult(removed);
    }

    /// <inheritdoc />
    public override Task<List<DatasourceDto>> GetDatasourcesAsync(string? tag = null)
    {
        IEnumerable<DatasourceDto> collection = _datasources.Values.AsEnumerable();

        List<DatasourceDto> sourceDatasources = string.IsNullOrWhiteSpace(tag)
            ? [.. collection.OrderBy(d => d.DisplayName)]
            :
            [
                .. collection
                    .Where(d =>
                        d.Tags != null && d.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)
                    )
                    .OrderBy(d => d.DisplayName),
            ];

        // Create copies to avoid modifying the stored objects
        List<DatasourceDto> datasources = sourceDatasources
            .Select(ds => new DatasourceDto
            {
                Id = ds.Id,
                Provider = ds.Provider,
                ConnectionString = null, // Do not return the encrypted connection string
                DisplayName = ds.DisplayName,
                Description = ds.Description,
                Tags = ds.Tags?.ToList() ?? [],
                IsEnabled = ds.IsEnabled,
                CreatedAt = ds.CreatedAt,
                UpdatedAt = ds.UpdatedAt,
            })
            .ToList();

        return Task.FromResult(datasources);
    }

    /// <inheritdoc />
    public override Task<DatasourceDto?> GetDatasourceAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (
            !_datasources.TryGetValue(id.ToLowerInvariant(), out DatasourceDto? datasource)
            || datasource == null
        )
        {
            return Task.FromResult<DatasourceDto?>(null);
        }

        // Create a copy to avoid modifying the stored object
        DatasourceDto result = new()
        {
            Id = datasource.Id,
            Provider = datasource.Provider,
            ConnectionString = null, // Do not return the encrypted connection string
            DisplayName = datasource.DisplayName,
            Description = datasource.Description,
            Tags = datasource.Tags?.ToList() ?? [],
            IsEnabled = datasource.IsEnabled,
            CreatedAt = datasource.CreatedAt,
            UpdatedAt = datasource.UpdatedAt,
        };

        return Task.FromResult<DatasourceDto?>(result);
    }

    /// <inheritdoc />
    public override Task<bool> DatasourceExistsAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        bool exists = _datasources.ContainsKey(id.ToLowerInvariant());
        return Task.FromResult(exists);
    }

    /// <inheritdoc />
    public override Task<string?> GetConnectionStringAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        _datasources.TryGetValue(id.ToLowerInvariant(), out DatasourceDto? datasource);
        string? encryptedConnectionString = datasource?.ConnectionString;

        try
        {
            return Task.FromResult(
                encryptedConnectionString != null
                    ? DecryptConnectionString(encryptedConnectionString)
                    : null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to decrypt connection string for datasource ID {DatasourceId}",
                id
            );
            return Task.FromResult<string?>(null);
        }
    }
}
