// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Reflection;

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

using Testcontainers.MsSql;
using Testcontainers.MySql;
using Testcontainers.PostgreSql;

namespace MJCZone.DapperMatic.AspNetCore.Tests;

/// <summary>
/// Testcontainers assembly fixture for setting up and tearing down test containers.
/// We use this because we do not want to intantiate containers over and over when running tests.
/// </summary>
public class TestcontainersAssemblyFixture : IAsyncLifetime
{
    public const string DatasourceId_SqlServer = "Test-SqlServer";
    public const string DatasourceId_MySql = "Test-MySQL";
    public const string DatasourceId_PostgreSql = "Test-PostgreSQL";
    public const string DatasourceId_Sqlite = "Test-SQLite";

    private readonly MsSqlContainer _sqlServerContainer;
    private readonly MySqlContainer _mySqlContainer;
    private readonly PostgreSqlContainer _postgreSqlContainer;
    private readonly string _tempFilePath;
    private readonly string _sqliteFilePath;

    public TestcontainersAssemblyFixture()
    {
        _sqlServerContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-CU13-ubuntu-22.04")
            .WithPassword("Strong_password_123!")
            .WithAutoRemove(true)
            .WithCleanUp(true)
            .Build();

        _mySqlContainer = new MySqlBuilder()
            .WithImage("mysql:8.4")
            .WithPassword("Strong_password_123!")
            .WithAutoRemove(true)
            .WithCleanUp(true)
            .Build();

        _postgreSqlContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .WithPassword("Strong_password_123!")
            .WithAutoRemove(true)
            .WithCleanUp(true)
            .Build();

        var uniqueHandle = Guid.NewGuid().ToString("N");

        // assembly directory
        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

        _tempFilePath = Path.Combine(assemblyDirectory, $"dappermatic-testds-{uniqueHandle}.json");
        _sqliteFilePath = Path.Combine(assemblyDirectory, $"dappermatic-testdb-{uniqueHandle}.db");
    }

    public virtual string SqlServerConnectionString => _sqlServerContainer.GetConnectionString();
    public virtual string MySqlConnectionString => _mySqlContainer.GetConnectionString();
    public virtual string PostgreSqlConnectionString => _postgreSqlContainer.GetConnectionString();
    public virtual string SqlServerContainerId => $"{_sqlServerContainer.Id}";
    public virtual string MySqlContainerId => $"{_mySqlContainer.Id}";
    public virtual string PostgreSqlContainerId => $"{_postgreSqlContainer.Id}";

    public virtual string TempFilePath => _tempFilePath;

    private List<DatasourceDto> _testDatasources = [];

    public virtual IReadOnlyList<DatasourceDto> GetTestDatasources(string? sqliteFile = null)
    {
        if (_testDatasources.Count > 0)
        {
            return _testDatasources.AsReadOnly();
        }

        _testDatasources =
        [
            new DatasourceDto
            {
                Id = DatasourceId_SqlServer,
                Provider = "SqlServer",
                ConnectionString = SqlServerConnectionString,
                DisplayName = "Test SQL Server",
                Description = "SQL Server test container",
                Tags = ["test", "sqlserver"],
            },
            new DatasourceDto
            {
                Id = DatasourceId_MySql,
                Provider = "MySql",
                ConnectionString = MySqlConnectionString,
                DisplayName = "Test MySQL",
                Description = "MySQL test container",
                Tags = ["test", "mysql"],
            },
            new DatasourceDto
            {
                Id = DatasourceId_PostgreSql,
                Provider = "PostgreSql",
                ConnectionString = PostgreSqlConnectionString,
                DisplayName = "Test PostgreSQL",
                Description = "PostgreSQL test container",
                Tags = ["test", "postgresql"],
            },
            // The problem with in-memory SQLite is that the connection must remain open for the lifetime of the database.
            // This makes it difficult to use in tests where connections are opened and closed frequently.
            // new DatasourceDto
            // {
            //     Id = DatasourceId_Sqlite,
            //     Provider = "Sqlite",
            //     ConnectionString = "Data Source=:memory:",
            //     DisplayName = "Test SQLite",
            //     Description = "In-memory SQLite database",
            //     Tags = ["test", "sqlite"],
            // },
            new DatasourceDto
            {
                Id = DatasourceId_Sqlite,
                Provider = "Sqlite",
                ConnectionString = new System.Data.SQLite.SQLiteConnectionStringBuilder
                {
                    DataSource = string.IsNullOrWhiteSpace(sqliteFile)
                        ? _sqliteFilePath
                        : sqliteFile,
                    // ForeignKeys = true,
                    // JournalMode = System.Data.SQLite.SQLiteJournalModeEnum.Off,
                    // SyncMode = System.Data.SQLite.SynchronizationModes.Full,
                    // Pooling = false,
                    // CacheSize = 0, // 10 MB
                    // PageSize = 4096,
                    // DefaultTimeout = 5000, // 5 seconds
                    FailIfMissing = false,
                }.ToString(),
                DisplayName = "Test SQLite",
                Description = "SQLite database file",
                Tags = ["test", "sqlite"],
            },
        ];
        return _testDatasources.AsReadOnly();
    }

    /// <summary>
    /// Gets a test datasource by provider type.
    /// </summary>
    /// <param name="provider">The database provider.</param>
    /// <returns>The test datasource or null if not found.</returns>
    public DatasourceDto? GetTestDatasource(string provider)
    {
        return _testDatasources.FirstOrDefault(ds => ds.Provider == provider);
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
                _sqlServerContainer.StartAsync(),
                _mySqlContainer.StartAsync(),
                _postgreSqlContainer.StartAsync()
            )
            .ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
                _sqlServerContainer.DisposeAsync().AsTask(),
                _mySqlContainer.DisposeAsync().AsTask(),
                _postgreSqlContainer.DisposeAsync().AsTask()
            )
            .ConfigureAwait(false);

        if (File.Exists(_tempFilePath))
        {
            try
            {
                File.Delete(_tempFilePath);
            }
            catch
            {
                // Ignore
            }
        }

        // get the sqlite files (.db, -shm, -wal, etc...)
        var sqliteFiles = Directory.GetFiles(
            Path.GetDirectoryName(_sqliteFilePath)!,
            Path.GetFileNameWithoutExtension(_sqliteFilePath) + "*"
        );
        foreach (var file in sqliteFiles)
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Ignore
            }
        }
    }
}
