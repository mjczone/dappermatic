// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MJCZone.DapperMatic.AspNetCore.Auditing;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using MJCZone.DapperMatic.AspNetCore.Tests.Infrastructure;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// Integration tests for DapperMatic audit logging functionality.
/// </summary>
public class DatasourceAuditTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public DatasourceAuditTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddDatasource_Success_LogsAuditEvent()
    {
        var auditLogger = new TestDapperMaticAuditLogger();
        using var client = CreateClientWithAuditLogger(auditLogger);

        var request = new DatasourceDto
        {
            Id = "AuditTest",
            Provider = "Sqlite",
            ConnectionString = "Data Source=test.db",
            DisplayName = "Audit Test Datasource",
        };

        var response = await client.PostAsJsonAsync("/api/dm/d/", request);
        response.Should().HaveStatusCode(HttpStatusCode.Created);

        // Verify audit event was logged
        auditLogger.AuditEvents.Should().HaveCount(1);
        var auditEvent = auditLogger.AuditEvents[0];
        auditEvent.Operation.Should().Be("datasources/add");
        auditEvent.DatasourceId.Should().Be("AuditTest");
        auditEvent.Success.Should().BeTrue();
        auditEvent.ErrorMessage.Should().BeNull();
        auditEvent.UserIdentifier.Should().NotBeNull();
    }

    [Fact]
    public async Task AddDatasource_Overwrite_LogsSuccessAuditEvent()
    {
        var auditLogger = new TestDapperMaticAuditLogger();
        using var client = CreateClientWithAuditLogger(auditLogger);

        var request = new DatasourceDto
        {
            Id = TestcontainersAssemblyFixture.DatasourceId_SqlServer, // This already exists in test data but gets overwritten
            Provider = "Sqlite",
            ConnectionString = "Data Source=test.db",
            DisplayName = "Overwritten Test",
        };
        var response = await client.PostAsJsonAsync("/api/dm/d/", request);
        response.Should().HaveStatusCode(HttpStatusCode.Conflict);

        // Now overwrite it
        response = await client.PutAsJsonAsync(
            $"/api/dm/d/{request.Id}",
            new DatasourceDto
            {
                Provider = "Sqlite",
                ConnectionString = "Data Source=test_overwrite.db",
                DisplayName = "Overwritten Test",
            }
        );
        response.Should().HaveStatusCode(HttpStatusCode.OK);

        // Verify success audit event was logged
        auditLogger.AuditEvents.Should().HaveCount(2);
        var auditEvent = auditLogger.AuditEvents[0];
        auditEvent.Operation.Should().Be("datasources/add");
        auditEvent.DatasourceId.Should().Be(TestcontainersAssemblyFixture.DatasourceId_SqlServer);
        auditEvent.Success.Should().BeFalse();
        auditEvent.ErrorMessage.Should().NotBeNull();

        auditEvent = auditLogger.AuditEvents[1];
        auditEvent.Operation.Should().Be("datasources/update");
        auditEvent.DatasourceId.Should().Be(TestcontainersAssemblyFixture.DatasourceId_SqlServer);
        auditEvent.Success.Should().BeTrue();
        auditEvent.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task GetDatasource_Success_LogsAuditEvent()
    {
        var auditLogger = new TestDapperMaticAuditLogger();
        using var client = CreateClientWithAuditLogger(auditLogger);

        var response = await client.GetAsync("/api/dm/d/Test-SqlServer");
        response.Should().HaveStatusCode(HttpStatusCode.OK);

        // Verify audit event was logged
        auditLogger.AuditEvents.Should().HaveCount(1);
        var auditEvent = auditLogger.AuditEvents[0];
        auditEvent.Operation.Should().Be("datasources/get");
        auditEvent.DatasourceId.Should().Be(TestcontainersAssemblyFixture.DatasourceId_SqlServer);
        auditEvent.Success.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateDatasource_Success_LogsAuditEvent()
    {
        var auditLogger = new TestDapperMaticAuditLogger();
        using var client = CreateClientWithAuditLogger(auditLogger);

        var request = new DatasourceDto { DisplayName = "Updated Test Name" };

        var response = await client.PutAsJsonAsync("/api/dm/d/Test-SqlServer", request);
        response.Should().HaveStatusCode(HttpStatusCode.OK);

        // Verify audit event was logged
        auditLogger.AuditEvents.Should().HaveCount(1);
        var auditEvent = auditLogger.AuditEvents[0];
        auditEvent.Operation.Should().Be("datasources/update");
        auditEvent.DatasourceId.Should().Be(TestcontainersAssemblyFixture.DatasourceId_SqlServer);
        auditEvent.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteDatasource_Success_LogsAuditEvent()
    {
        var auditLogger = new TestDapperMaticAuditLogger();
        using var client = CreateClientWithAuditLogger(auditLogger);

        var response = await client.DeleteAsync("/api/dm/d/Test-SqlServer");
        response.Should().HaveStatusCode(HttpStatusCode.OK);

        // Verify audit event was logged
        auditLogger.AuditEvents.Should().HaveCount(1);
        var auditEvent = auditLogger.AuditEvents[0];
        auditEvent.Operation.Should().Be("datasources/remove");
        auditEvent.DatasourceId.Should().Be(TestcontainersAssemblyFixture.DatasourceId_SqlServer);
        auditEvent.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ListDatasources_Success_LogsAuditEvent()
    {
        var auditLogger = new TestDapperMaticAuditLogger();
        using var client = CreateClientWithAuditLogger(auditLogger);

        var response = await client.GetAsync("/api/dm/d/");
        response.Should().HaveStatusCode(HttpStatusCode.OK);

        // Verify audit event was logged
        auditLogger.AuditEvents.Should().HaveCount(1);
        var auditEvent = auditLogger.AuditEvents[0];
        auditEvent.Operation.Should().Be("datasources/list");
        auditEvent.Success.Should().BeTrue();
        auditEvent.DatasourceId.Should().BeNull(); // List operation doesn't target specific datasource
    }

    private HttpClient CreateClientWithAuditLogger(TestDapperMaticAuditLogger auditLogger)
    {
        var factory = new WafWithInMemoryDatasourceRepository(
            _fixture.GetTestDatasources()
        ).WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace the default audit logger with our test logger
                services.RemoveAll<IDapperMaticAuditLogger>();
                services.AddSingleton<IDapperMaticAuditLogger>(auditLogger);
            });
        });

        return factory.CreateClient();
    }
}
