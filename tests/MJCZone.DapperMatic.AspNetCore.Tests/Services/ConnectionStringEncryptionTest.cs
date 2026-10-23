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
    public void Keys_can_be_rotated_and_only_persistent_repositories_need_one()
    {
        var oldKey = CryptoUtils.GenerateEncryptionKey();
        var newKey = CryptoUtils.GenerateEncryptionKey();
        var logger = new LoggerFactory().CreateLogger<InMemoryDapperMaticDatasourceRepository>();
        InMemoryDapperMaticDatasourceRepository Repository(string? key, params string[] previous)
        {
            var options = new DapperMaticOptions { ConnectionStringEncryptionKey = key };
            options.PreviousConnectionStringEncryptionKeys.AddRange(previous);
            return new InMemoryDapperMaticDatasourceRepository(Options.Create(options), new GuidDatasourceIdFactory(), logger);
        }

        // Each ciphertext names the key that made it, so that the current key and previous ones can be told apart.
        var encrypted = Repository(oldKey).EncryptConnectionString("Data Source=a.db");
        encrypted.Should().MatchRegex("^k[0-9a-f]{8}:");
        Repository(newKey, oldKey).DecryptConnectionString(encrypted).Should().Be("Data Source=a.db");
        var rotated = Repository(newKey, oldKey).EncryptConnectionString("Data Source=a.db");
        rotated[..9].Should().NotBe(encrypted[..9]);
        FluentActions.Invoking(() => Repository(newKey).DecryptConnectionString(encrypted)).Should().Throw<System.Security.Cryptography.CryptographicException>();

        // Ciphertexts written before key ids are tried with each key.
        Repository(newKey, oldKey).DecryptConnectionString(CryptoUtils.EncryptToBase64("Data Source=b.db", oldKey)).Should().Be("Data Source=b.db");

        // In memory, nothing outlives the process, so no key is needed; a file has to outlive it.
        var memory = Repository(null);
        memory.DecryptConnectionString(memory.EncryptConnectionString("Data Source=c.db")).Should().Be("Data Source=c.db");
        FluentActions
            .Invoking(() => new FileDapperMaticDatasourceRepository(
                Path.Combine(Path.GetTempPath(), $"dm-{Guid.NewGuid():N}.json"),
                new GuidDatasourceIdFactory(),
                Options.Create(new DapperMaticOptions()),
                new LoggerFactory().CreateLogger<FileDapperMaticDatasourceRepository>()
            ))
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*ConnectionStringEncryptionKey*");
    }

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
