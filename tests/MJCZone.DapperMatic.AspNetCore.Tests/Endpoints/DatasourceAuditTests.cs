// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
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
    public async Task Should_handle_datasource_operations_log_audit_events_Async()
    {
        var auditLogger = new TestDapperMaticAuditLogger();
        using var client = CreateClientWithAuditLogger(auditLogger);

        // List datasources - should log audit event
        var listResponse = await client.GetAsync("/api/dm/d/");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Get existing datasource - should log audit event
        var getResponse = await client.GetAsync($"/api/dm/d/{TestcontainersAssemblyFixture.DatasourceId_SqlServer}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Add new datasource - should log audit event
        var newDatasource = new DatasourceDto
        {
            Id = "AuditTest",
            Provider = "Sqlite",
            ConnectionString = "Data Source=test.db",
            DisplayName = "Audit Test Datasource",
        };
        var addResponse = await client.PostAsJsonAsync("/api/dm/d/", newDatasource);
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Add duplicate datasource - should log failure audit event
        var duplicateResponse = await client.PostAsJsonAsync("/api/dm/d/", newDatasource);
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Update datasource - should log audit event
        var updateRequest = new DatasourceDto { DisplayName = "Updated Test Name" };
        var updateResponse = await client.PutAsJsonAsync("/api/dm/d/AuditTest", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Delete datasource - should log audit event
        var deleteResponse = await client.DeleteAsync("/api/dm/d/AuditTest");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify all operations logged audit events
        auditLogger.AuditEvents.Should().HaveCountGreaterThanOrEqualTo(6);

        // Verify list operation
        var listEvents = auditLogger.GetEventsForOperation("datasources/list");
        listEvents.Should().HaveCount(1);
        listEvents[0].Success.Should().BeTrue();
        listEvents[0].DatasourceId.Should().BeNull();

        // Verify get operation
        var getEvents = auditLogger.GetEventsForOperation("datasources/get");
        getEvents.Should().HaveCount(1);
        getEvents[0].Success.Should().BeTrue();
        getEvents[0].DatasourceId.Should().Be(TestcontainersAssemblyFixture.DatasourceId_SqlServer);

        // Verify successful add operation
        var successfulAddEvents = auditLogger.GetEventsForOperation("datasources/post").Where(e => e.Success).ToList();
        successfulAddEvents.Should().HaveCount(1);
        successfulAddEvents[0].DatasourceId.Should().Be("AuditTest");
        successfulAddEvents[0].UserIdentifier.Should().NotBeNull();

        // Verify failed add operation (duplicate)
        var failedAddEvents = auditLogger.GetEventsForOperation("datasources/post").Where(e => !e.Success).ToList();
        failedAddEvents.Should().HaveCount(1);
        failedAddEvents[0].DatasourceId.Should().Be("AuditTest");
        failedAddEvents[0].Message.Should().NotBeNull();

        // Verify update operation
        var updateEvents = auditLogger.GetEventsForOperation("datasources/put");
        updateEvents.Should().HaveCount(1);
        updateEvents[0].DatasourceId.Should().Be("AuditTest");
        updateEvents[0].Success.Should().BeTrue();

        // Verify delete operation
        var deleteEvents = auditLogger.GetEventsForOperation("datasources/delete");
        deleteEvents.Should().HaveCount(1);
        deleteEvents[0].DatasourceId.Should().Be("AuditTest");
        deleteEvents[0].Success.Should().BeTrue();

        // Verify overall counts
        auditLogger.GetSuccessfulEvents().Should().HaveCountGreaterThanOrEqualTo(5); // list, get, add, update, delete
        auditLogger.GetFailedEvents().Should().HaveCount(1); // duplicate add
    }

    private HttpClient CreateClientWithAuditLogger(TestDapperMaticAuditLogger auditLogger)
    {
        var factory = new WafWithTestAuditLogger(_fixture.GetTestDatasources(), auditLogger);
        return factory.CreateClient();
    }
}
