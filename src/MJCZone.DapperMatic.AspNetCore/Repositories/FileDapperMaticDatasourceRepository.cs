// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Repositories;

/// <summary>
/// File-based implementation of IDapperMaticDatasourceRepository that stores datasources in a JSON file with encrypted
/// connection strings: <c>{ "datasources": [ ... ] }</c>, written whole on each change.
/// </summary>
public sealed class FileDapperMaticDatasourceRepository : DapperMaticDatasourceRepositoryBase, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _filePath;
    private readonly IDatasourceIdFactory _datasourceIdFactory;
    private readonly ILogger<FileDapperMaticDatasourceRepository> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<StoredDatasource>? _datasources;

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
        RequireEncryptionKey("a file repository");

        _filePath = Path.GetFullPath(filePath);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _datasourceIdFactory = datasourceIdFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public override void Initialize() => _ = Load();

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

        return await WriteAsync(datasources =>
            {
                if (Find(datasources, datasource.Id) is not null)
                {
                    return false; // Already exists
                }

                var now = DateTimeOffset.UtcNow;
                datasources.Add(
                    new StoredDatasource
                    {
                        Id = datasource.Id,
                        Provider = datasource.Provider,
                        ConnectionString = EncryptConnectionString(datasource.ConnectionString),
                        DisplayName = datasource.DisplayName,
                        Description = datasource.Description,
                        Tags = datasource.Tags?.ToList() ?? [],
                        IsEnabled = datasource.IsEnabled.GetValueOrDefault(true),
                        CreatedAt = now,
                        UpdatedAt = now,
                    }
                );
                return true;
            })
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<bool> UpdateDatasourceAsync(DatasourceDto datasource)
    {
        ArgumentNullException.ThrowIfNull(datasource);

        if (string.IsNullOrWhiteSpace(datasource.Id))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasource));
        }

        return await WriteAsync(datasources =>
            {
                if (Find(datasources, datasource.Id) is not { } existing)
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
                    existing.IsEnabled = datasource.IsEnabled.Value;
                }

                existing.UpdatedAt = DateTimeOffset.UtcNow;
                return true;
            })
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<bool> RemoveDatasourceAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return await WriteAsync(datasources => Find(datasources, id) is { } existing && datasources.Remove(existing))
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override Task<List<DatasourceDto>> GetDatasourcesAsync(string? tag = null)
    {
        List<DatasourceDto> datasources =
        [
            .. Load()
                .Where(d => string.IsNullOrWhiteSpace(tag) || d.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                .OrderBy(d => d.DisplayName)
                .Select(d => d.ToDto()),
        ];
        return Task.FromResult(datasources);
    }

    /// <inheritdoc />
    public override Task<DatasourceDto?> GetDatasourceAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return Task.FromResult(Find(Load(), id)?.ToDto());
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

        var encryptedConnectionString = Find(Load(), id)?.ConnectionString;
        if (string.IsNullOrWhiteSpace(encryptedConnectionString))
        {
            return Task.FromResult<string?>(null);
        }

        try
        {
            return Task.FromResult<string?>(DecryptConnectionString(encryptedConnectionString));
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
    public void Dispose() => _gate.Dispose();

    private static StoredDatasource? Find(List<StoredDatasource> datasources, string? id) =>
        datasources.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));

    // The datasources as last read or written. A snapshot: writes replace the list, never change it in place.
    private List<StoredDatasource> Load()
    {
        if (Volatile.Read(ref _datasources) is { } loaded)
        {
            return loaded;
        }

        List<StoredDatasource> datasources = [];
        if (File.Exists(_filePath))
        {
            using var stream = File.OpenRead(_filePath);
            if (stream.Length > 0)
            {
                datasources = JsonSerializer.Deserialize<StoredFile>(stream, JsonOptions)?.Datasources ?? [];
            }
        }

        Interlocked.CompareExchange(ref _datasources, datasources, null);
        return _datasources!;
    }

    // Changes a copy of the datasources and, if the change says so, writes the file whole (to a temporary file, then
    // moved over the old one) before publishing the copy.
    private async Task<bool> WriteAsync(Func<List<StoredDatasource>, bool> change)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var copy = Load().Select(d => d.Clone()).ToList();
            if (!change(copy))
            {
                return false;
            }

            var temporary = _filePath + ".tmp";
            await using (var stream = File.Create(temporary))
            {
                await JsonSerializer.SerializeAsync(stream, new StoredFile { Datasources = copy }, JsonOptions).ConfigureAwait(false);
            }

            File.Move(temporary, _filePath, overwrite: true);
            Volatile.Write(ref _datasources, copy);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed class StoredFile
    {
        public List<StoredDatasource> Datasources { get; set; } = [];
    }

    private sealed class StoredDatasource
    {
        public string? Id { get; set; }

        public string? Provider { get; set; }

        public string? ConnectionString { get; set; }

        public string? DisplayName { get; set; }

        public string? Description { get; set; }

        public List<string> Tags { get; set; } = [];

        public bool IsEnabled { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public StoredDatasource Clone()
        {
            var clone = (StoredDatasource)MemberwiseClone();
            clone.Tags = [.. Tags];
            return clone;
        }

        // The connection string is never returned.
        public DatasourceDto ToDto() =>
            new()
            {
                Id = Id,
                Provider = Provider,
                DisplayName = DisplayName,
                Description = Description,
                Tags = [.. Tags],
                IsEnabled = IsEnabled,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt,
            };
    }
}
