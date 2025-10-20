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

public class WafWithFileDatasourceRepository : WebApplicationFactory<Program>
{
    public static void DeleteDatasourcesFile()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var filePath = Path.Combine(assemblyDir, "test.datasources.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private static readonly string EncryptionKey = CryptoUtils.GenerateEncryptionKey();

    private readonly List<DatasourceDto>? _testDatasources;
    private readonly string _tempFilePath;

    public WafWithFileDatasourceRepository(IReadOnlyList<DatasourceDto> datasources)
    {
        _testDatasources = [.. datasources];

        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        _tempFilePath = Path.Combine(assemblyDir, "test.datasources.json");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddDapperMatic(dmb =>
            {
                dmb.UseFileDatasourceRepository(_tempFilePath);
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

    public string GetTempFilePath()
    {
        return _tempFilePath;
    }

    public void DeleteFile()
    {
        if (_tempFilePath != null && File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }
}
