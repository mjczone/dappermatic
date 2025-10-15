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
/// Flags to control which containers are initialized.
/// </summary>
[Flags]
public enum ContainerTypes
{
    None = 0,
    SqlServer = 1,
    MySql = 2,
    PostgreSql = 4,
    All = SqlServer | MySql | PostgreSql,
}

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

    private readonly ContainerTypes _containerTypes;
    private readonly MsSqlContainer? _sqlServerContainer;
    private readonly MySqlContainer? _mySqlContainer;
    private readonly PostgreSqlContainer? _postgreSqlContainer;
    private readonly string _tempFilePath;
    private readonly string _sqliteFilePath;

    public TestcontainersAssemblyFixture()
        : this(ContainerTypes.All) { }

    private TestcontainersAssemblyFixture(ContainerTypes containerTypes)
    {
        _containerTypes = containerTypes;

        if (containerTypes.HasFlag(ContainerTypes.SqlServer))
        {
            _sqlServerContainer = new MsSqlBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2022-CU13-ubuntu-22.04")
                .WithPassword("Strong_password_123!")
                .WithAutoRemove(true)
                .WithCleanUp(true)
                .Build();
        }

        if (containerTypes.HasFlag(ContainerTypes.MySql))
        {
            _mySqlContainer = new MySqlBuilder()
                .WithImage("mysql:8.4")
                .WithPassword("Strong_password_123!")
                .WithAutoRemove(true)
                .WithCleanUp(true)
                .Build();
        }

        if (containerTypes.HasFlag(ContainerTypes.PostgreSql))
        {
            _postgreSqlContainer = new PostgreSqlBuilder()
                .WithImage("postgres:16")
                .WithPassword("Strong_password_123!")
                .WithAutoRemove(true)
                .WithCleanUp(true)
                .Build();
        }

        var uniqueHandle = Guid.NewGuid().ToString("N");

        // assembly directory
        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

        _tempFilePath = Path.Combine(assemblyDirectory, $"dappermatic-testds-{uniqueHandle}.json");
        _sqliteFilePath = Path.Combine(assemblyDirectory, $"dappermatic-testdb-{uniqueHandle}.db");
    }

    public virtual string SqlServerConnectionString =>
        _sqlServerContainer?.GetConnectionString()
        ?? throw new InvalidOperationException("SQL Server container not initialized");
    public virtual string MySqlConnectionString =>
        _mySqlContainer?.GetConnectionString()
        ?? throw new InvalidOperationException("MySQL container not initialized");
    public virtual string PostgreSqlConnectionString =>
        _postgreSqlContainer?.GetConnectionString()
        ?? throw new InvalidOperationException("PostgreSQL container not initialized");
    public virtual string SqlServerContainerId =>
        _sqlServerContainer?.Id
        ?? throw new InvalidOperationException("SQL Server container not initialized");
    public virtual string MySqlContainerId =>
        _mySqlContainer?.Id
        ?? throw new InvalidOperationException("MySQL container not initialized");
    public virtual string PostgreSqlContainerId =>
        _postgreSqlContainer?.Id
        ?? throw new InvalidOperationException("PostgreSQL container not initialized");

    public virtual string TempFilePath => _tempFilePath;

    private List<DatasourceDto> _testDatasources = [];

    public virtual IReadOnlyList<DatasourceDto> GetTestDatasources(string? sqliteFile = null)
    {
        if (_testDatasources.Count > 0)
        {
            return _testDatasources.AsReadOnly();
        }

        var datasources = new List<DatasourceDto>();

        if (_containerTypes.HasFlag(ContainerTypes.SqlServer))
        {
            datasources.Add(
                new DatasourceDto
                {
                    Id = DatasourceId_SqlServer,
                    Provider = "SqlServer",
                    ConnectionString = SqlServerConnectionString,
                    DisplayName = "Test SQL Server",
                    Description = "SQL Server test container",
                    Tags = ["test", "sqlserver"],
                }
            );
        }

        if (_containerTypes.HasFlag(ContainerTypes.MySql))
        {
            datasources.Add(
                new DatasourceDto
                {
                    Id = DatasourceId_MySql,
                    Provider = "MySql",
                    ConnectionString = MySqlConnectionString,
                    DisplayName = "Test MySQL",
                    Description = "MySQL test container",
                    Tags = ["test", "mysql"],
                }
            );
        }

        if (_containerTypes.HasFlag(ContainerTypes.PostgreSql))
        {
            datasources.Add(
                new DatasourceDto
                {
                    Id = DatasourceId_PostgreSql,
                    Provider = "PostgreSql",
                    ConnectionString = PostgreSqlConnectionString,
                    DisplayName = "Test PostgreSQL",
                    Description = "PostgreSQL test container",
                    Tags = ["test", "postgresql"],
                }
            );
        }

        // SQLite is always available (no container needed)
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
        datasources.Add(
            new DatasourceDto
            {
                Id = DatasourceId_Sqlite,
                Provider = "Sqlite",
                ConnectionString = new System.Data.SQLite.SQLiteConnectionStringBuilder
                {
                    DataSource = string.IsNullOrWhiteSpace(sqliteFile)
                        ? _sqliteFilePath
                        : sqliteFile,
                    ForeignKeys = true,
                    BinaryGUID = false,
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
            }
        );

        _testDatasources = datasources;
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
        var tasks = new List<Task>();

        if (_sqlServerContainer != null)
            tasks.Add(_sqlServerContainer.StartAsync());

        if (_mySqlContainer != null)
            tasks.Add(_mySqlContainer.StartAsync());

        if (_postgreSqlContainer != null)
            tasks.Add(_postgreSqlContainer.StartAsync());

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    public async Task DisposeAsync()
    {
        var tasks = new List<Task>();

        if (_sqlServerContainer != null)
            tasks.Add(_sqlServerContainer.DisposeAsync().AsTask());

        if (_mySqlContainer != null)
            tasks.Add(_mySqlContainer.DisposeAsync().AsTask());

        if (_postgreSqlContainer != null)
            tasks.Add(_postgreSqlContainer.DisposeAsync().AsTask());

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

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
