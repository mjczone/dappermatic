// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.AspNetCore.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Services;

/// <summary>
/// Test to verify connection string encryption/decryption works correctly.
/// /// </summary>
public class ConnectionStringEncryptionTest
{
    [Fact]
    public async Task Should_require_repository_encrypts_and_decrypts_connection_strings_correctly_Async()
    {
        // Arrange
        var encryptionKey = CryptoUtils.GenerateEncryptionKey();
        var options = new DapperMaticOptions { ConnectionStringEncryptionKey = encryptionKey };
        var optionsWrapper = Options.Create(options);

        var logger = new LoggerFactory().CreateLogger<InMemoryDapperMaticDatasourceRepository>();
        var idFactory = new GuidDatasourceIdFactory();

        var repository = new InMemoryDapperMaticDatasourceRepository(optionsWrapper, idFactory, logger);

        var testDatasource = new DatasourceDto
        {
            Id = "Test-Encryption",
            Provider = "Sqlite",
            ConnectionString = "Data Source=:memory:",
            DisplayName = "Test Encryption",
            Description = "Test datasource for encryption",
        };

        // Act
        var addResult = await repository.AddDatasourceAsync(testDatasource);
        var retrievedConnectionString = await repository.GetConnectionStringAsync("Test-Encryption");

        // Assert
        addResult.Should().BeTrue();
        retrievedConnectionString.Should().NotBeNull();
        retrievedConnectionString.Should().Be("Data Source=:memory:");
    }

    [Fact]
    public async Task Should_require_repository_handles_null_connection_string_correctly_Async()
    {
        // Arrange
        var encryptionKey = CryptoUtils.GenerateEncryptionKey();
        var options = new DapperMaticOptions { ConnectionStringEncryptionKey = encryptionKey };
        var optionsWrapper = Options.Create(options);

        var logger = new LoggerFactory().CreateLogger<InMemoryDapperMaticDatasourceRepository>();
        var idFactory = new GuidDatasourceIdFactory();

        var repository = new InMemoryDapperMaticDatasourceRepository(optionsWrapper, idFactory, logger);

        // Act
        var retrievedConnectionString = await repository.GetConnectionStringAsync("NonExistent");

        // Assert
        retrievedConnectionString.Should().BeNull();
    }
}
