// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using JsonFlatFileDataStore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Repositories;

/// <summary>
/// File-based implementation of IDapperMaticDatasourceRepository that stores datasources in a JSON file with encrypted connection strings.
/// </summary>
public sealed class FileDapperMaticDatasourceRepository : DapperMaticDatasourceRepositoryBase, IDisposable
{
    private readonly DataStore _dataStore;
    private readonly IDatasourceIdFactory _datasourceIdFactory;
    private readonly ILogger<FileDapperMaticDatasourceRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileDapperMaticDatasourceRepository"/> class.
    /// </summary>
    /// <param name="filePath">The path to the JSON file where datasources will be stored.</param>
    /// <param name="datasourceIdFactory">The factory to generate datasource IDs.</param>
    /// <param name="options">The DapperMatic options containing the encryption key.</param>
    /// <param name="logger">The logger instance.</param>
    public FileDapperMaticDatasourceRepository(
        string filePath,
        IDatasourceIdFactory datasourceIdFactory,
        IOptions<DapperMaticOptions> options,
        ILogger<FileDapperMaticDatasourceRepository> logger
    )
        : base(options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        filePath = Path.GetFullPath(filePath);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _dataStore = new DataStore(filePath);
        _datasourceIdFactory = datasourceIdFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        // Ensure the collection exists
        GetCollection();
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
            throw new ArgumentException("Datasource connection string is required.", nameof(datasource));
        }

        if (await DatasourceExistsAsync(datasource.Id).ConfigureAwait(false))
        {
            return false; // Already exists
        }

        // Store the connection string encrypted
        var datasourceCopy = new DatasourceDto
        {
            Id = datasource.Id,
            Provider = datasource.Provider,
            ConnectionString = EncryptConnectionString(datasource.ConnectionString),
            DisplayName = datasource.DisplayName,
            Description = datasource.Description,
            Tags = datasource.Tags?.ToList() ?? [],
            IsEnabled = datasource.IsEnabled.GetValueOrDefault(true),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        return await GetCollection().InsertOneAsync(datasourceCopy).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<bool> UpdateDatasourceAsync(DatasourceDto datasource)
    {
        ArgumentNullException.ThrowIfNull(datasource);

        if (string.IsNullOrWhiteSpace(datasource.Id))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasource));
        }

        // Fetch existing to preserve non-updated fields
        var existing = await GetDatasourceAsync(datasource.Id).ConfigureAwait(false);
        if (existing == null)
        {
            return false; // Doesn't exist
        }

        if (datasource.Provider != null)
        {
            existing.Provider = datasource.Provider;
        }

        if (!string.IsNullOrWhiteSpace(datasource.ConnectionString))
        {
            existing.ConnectionString = EncryptConnectionString(datasource.ConnectionString);
        }

        if (!string.IsNullOrWhiteSpace(datasource.DisplayName))
        {
            existing.DisplayName = datasource.DisplayName;
        }

        if (!string.IsNullOrWhiteSpace(datasource.Description))
        {
            existing.Description = datasource.Description;
        }

        if (datasource.Tags != null && datasource.Tags.Count != 0)
        {
            existing.Tags = [.. datasource.Tags];
        }

        if (datasource.IsEnabled != null)
        {
            existing.IsEnabled = datasource.IsEnabled;
        }

        existing.UpdatedAt = DateTimeOffset.UtcNow;

        return await GetCollection()
            .ReplaceOneAsync(
                d => d.Id != null && d.Id.Equals(existing.Id, StringComparison.OrdinalIgnoreCase),
                existing
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<bool> RemoveDatasourceAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var removed = await GetCollection()
            .DeleteOneAsync(d => d.Id != null && d.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            .ConfigureAwait(false);

        return removed;
    }

    /// <inheritdoc />
    public override Task<List<DatasourceDto>> GetDatasourcesAsync(string? tag = null)
    {
        var collection = GetCollection();

        List<DatasourceDto> datasources = string.IsNullOrWhiteSpace(tag)
            ? [.. collection.AsQueryable().OrderBy(d => d.DisplayName)]
            :
            [
                .. collection
                    .AsQueryable()
                    .Where(d => d.Tags != null && d.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                    .OrderBy(d => d.DisplayName),
            ];

        foreach (var ds in datasources)
        {
            // Do not return the encrypted connection string
            ds.ConnectionString = null;
        }

        return Task.FromResult(datasources);
    }

    /// <inheritdoc />
    public override Task<DatasourceDto?> GetDatasourceAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var datasource = GetCollection()
            .AsQueryable()
            .FirstOrDefault(d => d.Id != null && d.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (datasource != null)
        {
            // Do not return the encrypted connection string
            datasource.ConnectionString = null;
        }

        return Task.FromResult(datasource);
    }

    /// <inheritdoc />
    public override async Task<bool> DatasourceExistsAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return await GetDatasourceAsync(id).ConfigureAwait(false) != null;
    }

    /// <inheritdoc />
    public override Task<string?> GetConnectionStringAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var datasource = GetCollection()
            .AsQueryable()
            .FirstOrDefault(d => d.Id != null && d.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (datasource == null)
        {
            return Task.FromResult<string?>(null);
        }

        var encryptedConnectionString = datasource.ConnectionString;

        try
        {
            return Task.FromResult(
                !string.IsNullOrWhiteSpace(encryptedConnectionString)
                    ? DecryptConnectionString(encryptedConnectionString)
                    : null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt connection string for datasource ID {DatasourceId}", id);
            return Task.FromResult<string?>(null);
        }
    }

    /// <summary>
    /// Releases all resources used by the current instance of the <see cref="FileDapperMaticDatasourceRepository"/> class.
    /// </summary>
    public void Dispose()
    {
        _dataStore?.Dispose();
    }

    private IDocumentCollection<DatasourceDto> GetCollection()
    {
        return _dataStore.GetCollection<DatasourceDto>("datasources");
    }
}
