// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.AspNetCore.Repositories;

/// <summary>
/// Database-based implementation of IDapperMaticDatasourceRepository that stores datasources in a database table with encrypted connection strings.
/// </summary>
public sealed class DatabaseDapperMaticDatasourceRepository : DapperMaticDatasourceRepositoryBase
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IDatasourceIdFactory _datasourceIdFactory;
    private readonly ILogger<DatabaseDapperMaticDatasourceRepository> _logger;
    private readonly string _provider;
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseDapperMaticDatasourceRepository"/> class.
    /// </summary>
    /// <param name="provider">The database provider for the repository storage.</param>
    /// <param name="connectionString">The connection string for the repository database.</param>
    /// <param name="connectionFactory">The connection factory for creating database connections.</param>
    /// <param name="datasourceIdFactory">The factory for generating datasource IDs.</param>
    /// <param name="options">The DapperMatic options containing the encryption key.</param>
    /// <param name="logger">The logger instance.</param>
    public DatabaseDapperMaticDatasourceRepository(
        string provider,
        string connectionString,
        IDbConnectionFactory connectionFactory,
        IDatasourceIdFactory datasourceIdFactory,
        IOptions<DapperMaticOptions> options,
        ILogger<DatabaseDapperMaticDatasourceRepository> logger
    )
        : base(options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(connectionFactory);

        _provider = provider;
        _connectionString = connectionString;
        _connectionFactory = connectionFactory;
        _datasourceIdFactory = datasourceIdFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        var tableModel = DmTableFactory.GetTable(typeof(DatabaseDatasource));
        using var connection = _connectionFactory.CreateConnection(_provider, _connectionString);
        connection.CreateTableIfNotExistsAsync(tableModel).GetAwaiter().GetResult();
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

        using var connection = _connectionFactory.CreateConnection(_provider, _connectionString);

        var sql =
            @"
                INSERT INTO dm_datasources (id, provider, encrypted_connection_string, display_name, description, tags, is_enabled, created_at, updated_at)
                VALUES (@Id, @Provider, @EncryptedConnectionString, @DisplayName, @Description, @Tags, @IsEnabled, @CreatedAt, @UpdatedAt)";

        var parameters = new
        {
            Id = datasource.Id.ToLowerInvariant(),
            datasource.Provider,
            EncryptedConnectionString = EncryptConnectionString(datasource.ConnectionString),
            datasource.DisplayName,
            datasource.Description,
            Tags = $";{string.Join(";", datasource.Tags ?? [])};".Replace(
                ";;",
                string.Empty,
                StringComparison.OrdinalIgnoreCase
            ),
            datasource.IsEnabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var rowsAffected = await connection.ExecuteAsync(sql, parameters).ConfigureAwait(false);
        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public override async Task<bool> UpdateDatasourceAsync(DatasourceDto datasource)
    {
        ArgumentNullException.ThrowIfNull(datasource);

        if (string.IsNullOrWhiteSpace(datasource.Id))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasource));
        }

        if (!await DatasourceExistsAsync(datasource.Id).ConfigureAwait(false))
        {
            return false; // Doesn't exist
        }

        using var connection = _connectionFactory.CreateConnection(_provider, _connectionString);

        var sql = new StringBuilder();
        sql.Append("UPDATE dm_datasources SET ");

        if (datasource.Provider != null)
        {
            sql.Append("provider = @Provider, ");
        }

        if (!string.IsNullOrWhiteSpace(datasource.ConnectionString))
        {
            sql.Append("encrypted_connection_string = @EncryptedConnectionString, ");
        }

        if (!string.IsNullOrWhiteSpace(datasource.DisplayName))
        {
            sql.Append("display_name = @DisplayName, ");
        }

        if (!string.IsNullOrWhiteSpace(datasource.Description))
        {
            sql.Append("description = @Description, ");
        }

        if (datasource.Tags != null && datasource.Tags.Count != 0)
        {
            sql.Append("tags = @Tags, ");
        }

        if (datasource.IsEnabled != null)
        {
            sql.Append("is_enabled = @IsEnabled, ");
        }

        sql.Append("updated_at = @UpdatedAt ");
        sql.Append("WHERE id = @Id");

        var parameters = new
        {
            Id = datasource.Id.ToLowerInvariant(),
            datasource.Provider,
            EncryptedConnectionString = string.IsNullOrWhiteSpace(datasource.ConnectionString)
                ? null
                : EncryptConnectionString(datasource.ConnectionString),
            DisplayName = string.IsNullOrWhiteSpace(datasource.DisplayName)
                ? null
                : datasource.DisplayName,
            Description = string.IsNullOrWhiteSpace(datasource.Description)
                ? null
                : datasource.Description,
            Tags = $";{string.Join(";", datasource.Tags ?? [])};".Replace(
                ";;",
                string.Empty,
                StringComparison.OrdinalIgnoreCase
            ),
            datasource.IsEnabled,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var rowsAffected = await connection
            .ExecuteAsync(sql.ToString(), parameters)
            .ConfigureAwait(false);
        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public override async Task<bool> RemoveDatasourceAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        using var connection = _connectionFactory.CreateConnection(_provider, _connectionString);

        var sql = "DELETE FROM dm_datasources WHERE id = @Id";
        var rowsAffected = await connection
            .ExecuteAsync(sql, new { Id = id.ToLowerInvariant() })
            .ConfigureAwait(false);
        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public override async Task<List<DatasourceDto>> GetDatasourcesAsync(string? tag = null)
    {
        using var connection = _connectionFactory.CreateConnection(_provider, _connectionString);

        var sql = "SELECT * FROM dm_datasources";
        if (!string.IsNullOrWhiteSpace(tag))
        {
            sql += " WHERE LOWER(tags) LIKE @TagPattern ORDER BY display_name";
        }
        var results = await connection
            .QueryAsync<DatabaseDatasource>(sql, new { TagPattern = $"%;{tag};%" })
            .ConfigureAwait(false);

        return
        [
            .. results
                .Select(r => new DatasourceDto
                {
                    Id = r.id!,
                    Provider = r.provider,
                    ConnectionString = null, // Do not expose connection string
                    DisplayName = r.display_name,
                    Description = r.description,
                    Tags = !string.IsNullOrEmpty(r.tags)
                        ? r
                            .tags.Split(
                                ';',
                                StringSplitOptions.RemoveEmptyEntries
                                    | StringSplitOptions.TrimEntries
                            )
                            .ToList()
                        : null,
                    IsEnabled = r.is_enabled,
                    CreatedAt = r.created_at,
                    UpdatedAt = r.updated_at,
                })
                .OrderBy(d => d.Id),
        ];
    }

    /// <inheritdoc />
    public override async Task<DatasourceDto?> GetDatasourceAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        using var connection = _connectionFactory.CreateConnection(_provider, _connectionString);

        var sql = "SELECT * FROM dm_datasources WHERE id = @Id";
        var result = await connection
            .QuerySingleOrDefaultAsync<DatabaseDatasource>(sql, new { Id = id.ToLowerInvariant() })
            .ConfigureAwait(false);

        if (result == null)
        {
            return null;
        }

        return new DatasourceDto
        {
            Id = result.id!,
            Provider = result.provider,
            ConnectionString = null, // Do not expose connection string
            DisplayName = result.display_name,
            Description = result.description,
            Tags = !string.IsNullOrEmpty(result.tags)
                ? result
                    .tags.Split(
                        ';',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                    )
                    .ToList()
                : null,
            IsEnabled = result.is_enabled,
            CreatedAt = result.created_at,
            UpdatedAt = result.updated_at,
        };
    }

    /// <inheritdoc />
    public override async Task<bool> DatasourceExistsAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        using var connection = _connectionFactory.CreateConnection(_provider, _connectionString);

        var sql = "SELECT COUNT(1) FROM dm_datasources WHERE id = @Id";
        var count = await connection
            .ExecuteScalarAsync<int>(sql, new { Id = id.ToLowerInvariant() })
            .ConfigureAwait(false);
        return count > 0;
    }

    /// <inheritdoc />
    public override async Task<string?> GetConnectionStringAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        using var connection = _connectionFactory.CreateConnection(_provider, _connectionString);

        var sql = "SELECT encrypted_connection_string FROM dm_datasources WHERE id = @Id";
        var encryptedConnectionString = await connection
            .QuerySingleOrDefaultAsync<string>(sql, new { Id = id.ToLowerInvariant() })
            .ConfigureAwait(false);

        try
        {
            return encryptedConnectionString != null
                ? DecryptConnectionString(encryptedConnectionString)
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to decrypt connection string for datasource ID {DatasourceId}",
                id
            );
            return null;
        }
    }

#pragma warning disable SA1600
#pragma warning disable SA1300
#pragma warning disable IDE1006
    /// <summary>
    /// Represents a database table for storing DapperMatic datasources.
    /// </summary>
    [DmTable(null, "dm_datasources")]
    private sealed class DatabaseDatasource
    {
        [DmColumn("id", isPrimaryKey: true, isNullable: false)]
        public required string id { get; set; }

        [DmColumn("provider", isNullable: false)]
        public required string provider { get; set; }

        [DmColumn("encrypted_connection_string", length: int.MaxValue, isNullable: false)]
        public required string encrypted_connection_string { get; set; }

        [DmColumn("display_name", length: 256, isNullable: false)]
        public required string display_name { get; set; }

        [DmColumn("description", length: 512, isNullable: true)]
        public string? description { get; set; }

        [DmColumn("tags", length: 2048, isNullable: true)]
        public string? tags { get; set; }

        [DmColumn("is_enabled", isNullable: false)]
        public bool is_enabled { get; set; }

        public DateTimeOffset created_at { get; set; }

        public DateTimeOffset updated_at { get; set; }
    }
#pragma warning restore IDE1006
#pragma warning restore SA1300
#pragma warning restore SA1600
}
