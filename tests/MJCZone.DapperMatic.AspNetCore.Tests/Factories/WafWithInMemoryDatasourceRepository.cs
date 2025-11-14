// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc.Testing;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Factories;

public class WafWithInMemoryDatasourceRepository(IReadOnlyList<DatasourceDto> datasources)
    : WebApplicationFactory<Program>
{
    private static readonly string EncryptionKey = CryptoUtils.GenerateEncryptionKey();

    private readonly List<DatasourceDto>? _testDatasources = [.. datasources];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Configure options for test-specific datasources
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
}
