// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MJCZone.DapperMatic.AspNetCore;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Tests.Infrastructure;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Factories;

public class WafWithDatabaseDatasourceRepository : WebApplicationFactory<Program>
{
    public static void DeleteDatabaseFile()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var filePath = Path.Combine(assemblyDir, "test.datasources.db");
        if (File.Exists(filePath))
        {
            try
            {
                // Force close any SQLite connections and delete
                GC.Collect();
                GC.WaitForPendingFinalizers();
                File.SetAttributes(filePath, FileAttributes.Normal);
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to delete database file: {ex.Message}");
                // If deletion fails, try to clear the file content at least
                try
                {
                    File.WriteAllText(filePath, string.Empty);
                }
                catch
                {
                    // Ignore if we can't even clear it
                }
            }
        }
    }

    // Use a static encryption key so all instances can decrypt each other's data
    private static readonly string EncryptionKey = CryptoUtils.GenerateEncryptionKey();

    private readonly List<DatasourceDto>? _testDatasources;
    private readonly string _connectionString;
    private readonly string _databaseFile;

    public WafWithDatabaseDatasourceRepository(
        IReadOnlyList<DatasourceDto> datasources,
        string? customDatabaseFileName = null
    )
    {
        _testDatasources = [.. datasources];

        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var databaseFileName = customDatabaseFileName ?? "test.datasources.db";
        _databaseFile = Path.Combine(assemblyDir, databaseFileName);
        _connectionString = $"Data Source={_databaseFile};";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddDapperMatic(dmb =>
            {
                dmb.UseDatabaseDatasourceRepository("Sqlite", _connectionString);
            });

            // Configure options
            services.Configure<DapperMaticOptions>(options =>
            {
                options.ConnectionStringEncryptionKey = EncryptionKey;

                if (_testDatasources != null && _testDatasources.Count != 0)
                {
                    options.Datasources.AddRange(_testDatasources);
                }
            });
        });

        builder.UseEnvironment("Testing");

        // Suppress logging noise during tests
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Warning);
        });
    }

    public void DeleteDatabase()
    {
        if (_databaseFile != null && File.Exists(_databaseFile))
        {
            try
            {
                // Force close any SQLite connections and delete
                GC.Collect();
                GC.WaitForPendingFinalizers();
                File.SetAttributes(_databaseFile, FileAttributes.Normal);
                File.Delete(_databaseFile);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to delete database file: {ex.Message}");
                // If deletion fails, try to clear the file content at least
                try
                {
                    File.WriteAllText(_databaseFile, string.Empty);
                }
                catch
                {
                    // Ignore if we can't even clear it
                }
            }
        }
    }
}
