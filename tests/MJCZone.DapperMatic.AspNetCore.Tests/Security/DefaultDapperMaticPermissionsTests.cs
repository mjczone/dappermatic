// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Security;

/// <summary>
/// The default permissions decide by the policies, and refuse what none grants.
/// </summary>
public class DefaultDapperMaticPermissionsTests
{
    [Fact]
    public async Task Nothing_is_allowed_without_a_policy_Async()
    {
        var permissions = Permissions(new DapperMaticOptions());
        (await permissions.IsAuthorizedAsync(Operation("datasources/list"))).Should().BeFalse();
        (await permissions.IsAuthorizedAsync(Operation("tables/post", "ds", authenticated: true, roles: ["admin"]))).Should().BeFalse();
    }

    [Fact]
    public async Task Require_role_grants_everything_and_read_only_role_grants_reading_Async()
    {
        var permissions = Permissions(new DapperMaticOptions { RequireRole = "DataAdmin", ReadOnlyRole = "DataReader" });

        (await permissions.IsAuthorizedAsync(Operation("datasources/post", "new", authenticated: true, roles: ["DataAdmin"]))).Should().BeTrue();
        (await permissions.IsAuthorizedAsync(Operation("datasources/get", "ds", authenticated: true, roles: ["DataReader"]))).Should().BeTrue();
        (await permissions.IsAuthorizedAsync(Operation("tables/list", "ds", authenticated: true, roles: ["DataReader"]))).Should().BeTrue();
        (await permissions.IsAuthorizedAsync(Operation("datasources/post", "new", authenticated: true, roles: ["DataReader"]))).Should().BeFalse();
        (await permissions.IsAuthorizedAsync(Operation("tables/delete", "ds", authenticated: true, roles: ["DataReader"]))).Should().BeFalse();
        (await permissions.IsAuthorizedAsync(Operation("datasources/get", "ds", authenticated: true, roles: ["User"]))).Should().BeFalse();
        (await permissions.IsAuthorizedAsync(Operation("datasources/get", "ds"))).Should().BeFalse();
    }

    [Fact]
    public async Task Policies_grant_by_role_datasource_schema_and_table_Async()
    {
        var options = new DapperMaticOptions();
        options.Policies.Add(DmPolicy.Allow(DmAction.ReadSchema, "authenticated").OnDatasources("tenant-*"));
        options.Policies.Add(DmPolicy.Allow(DmAction.ChangeSchema, "designer").InSchemas("sales").ExceptTables("audit*"));
        var permissions = Permissions(options);

        (await permissions.IsAuthorizedAsync(Operation("tables/list", "tenant-a", authenticated: true))).Should().BeTrue();
        (await permissions.IsAuthorizedAsync(Operation("tables/list", "billing", authenticated: true))).Should().BeFalse();
        (await permissions.IsAuthorizedAsync(Operation("tables/list", "tenant-a"))).Should().BeFalse();

        // A listing of datasources is about every one: a grant on some lets it through, and the list is narrowed.
        (await permissions.IsAuthorizedAsync(Operation("datasources/list", authenticated: true))).Should().BeTrue();

        (await permissions.IsAuthorizedAsync(Operation("tables/put", "ds", "sales", "orders", authenticated: true, roles: ["designer"]))).Should().BeTrue();
        (await permissions.IsAuthorizedAsync(Operation("tables/put", "ds", "sales", "audit_log", authenticated: true, roles: ["designer"]))).Should().BeFalse();
        (await permissions.IsAuthorizedAsync(Operation("tables/put", "ds", "hr", "orders", authenticated: true, roles: ["designer"]))).Should().BeFalse();
    }

    [Fact]
    public async Task Rows_are_decided_with_the_table_so_row_operations_pass_this_gate_Async()
    {
        var permissions = Permissions(new DapperMaticOptions());
        (await permissions.IsAuthorizedAsync(Operation("tables/query", "ds", null, "orders"))).Should().BeTrue();
        (await permissions.IsAuthorizedAsync(Operation("tables/insert-rows", "ds", null, "orders"))).Should().BeTrue();
    }

    [Fact]
    public void Operations_map_to_actions()
    {
        DapperMaticPolicySet.ActionOf("tables/query").Should().Be(DmAction.Read);
        DapperMaticPolicySet.ActionOf("views/query").Should().Be(DmAction.Read);
        DapperMaticPolicySet.ActionOf("tables/insert-rows").Should().Be(DmAction.Create);
        DapperMaticPolicySet.ActionOf("tables/update-rows").Should().Be(DmAction.Update);
        DapperMaticPolicySet.ActionOf("tables/delete-rows").Should().Be(DmAction.Delete);
        DapperMaticPolicySet.ActionOf("datasources/get").Should().Be(DmAction.ReadSchema);
        DapperMaticPolicySet.ActionOf("datasources/test").Should().Be(DmAction.ManageDatasources);
        DapperMaticPolicySet.ActionOf("datasources/delete").Should().Be(DmAction.ManageDatasources);
        DapperMaticPolicySet.ActionOf("columns/post").Should().Be(DmAction.ChangeSchema);
        DapperMaticPolicySet.ActionOf("datatypes/list").Should().Be(DmAction.ReadSchema);
        DapperMaticPolicySet.ActionOf("tables/exists").Should().Be(DmAction.ReadSchema);
        // An operation DapperMatic does not know, or a context no endpoint filled, is refused.
        DapperMaticPolicySet.ActionOf("something/else").Should().Be(DmAction.None);
        DapperMaticPolicySet.ActionOf(null).Should().Be(DmAction.None);
    }

    [Fact]
    public void Policies_bind_from_configuration_and_are_validated()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DapperMatic:Policies:0:Name"] = "tenant rows",
                    ["DapperMatic:Policies:0:Actions"] = "Read, Create",
                    ["DapperMatic:Policies:0:Roles:0"] = "authenticated",
                    ["DapperMatic:Policies:0:WithColumns:0"] = "tenant_id",
                    ["DapperMatic:Policies:0:Filter"] = "tenant_id eq @tenant.id",
                    ["DapperMatic:Policies:0:Presets:tenant_id"] = "@tenant.id",
                    ["DapperMatic:Policies:1:Kind"] = "Restrictive",
                    ["DapperMatic:Policies:1:Actions"] = "ReadWrite",
                    ["DapperMatic:Policies:1:ExcludeColumns:0"] = "password_hash",
                }
            )
            .Build();
        var options = new DapperMaticOptions();
        configuration.GetSection("DapperMatic").Bind(options);

        var policies = new DapperMaticPolicySet(Options.Create(options)).Policies;
        policies.Should().HaveCount(2);
        policies[0].Actions.Should().Be(DmAction.Read | DmAction.Create);
        policies[0].Presets["tenant_id"].Should().Be("@tenant.id");
        policies[0].Source.Should().Be("configuration");
        policies[1].Kind.Should().Be(DmPolicyKind.Restrictive);

        // A grant to nobody in particular, or a filter reading another table, is refused at startup.
        var nobody = new DapperMaticOptions();
        nobody.Policies.Add(new DmPolicy { Actions = DmAction.Read });
        var act = () => new DapperMaticPolicySet(Options.Create(nobody));
        act.Should().Throw<InvalidOperationException>().WithMessage("*grants to nobody*");

        var related = new DapperMaticOptions();
        related.Policies.Add(DmPolicy.Allow(DmAction.Read, "*").Where("customer/country eq 'FR'"));
        act = () => new DapperMaticPolicySet(Options.Create(related));
        act.Should().Throw<InvalidOperationException>().WithMessage("*another table*");
    }

    private static DefaultDapperMaticPermissions Permissions(DapperMaticOptions options) => new(new DapperMaticPolicySet(Options.Create(options)));

    private static DapperMaticOperation Operation(
        string operation,
        string? datasourceId = null,
        string? schemaName = null,
        string? tableName = null,
        bool authenticated = false,
        string[]? roles = null
    )
    {
        ClaimsPrincipal? user = null;
        if (authenticated)
        {
            var claims = new List<Claim> { new(ClaimTypes.Name, "testuser") };
            claims.AddRange((roles ?? []).Select(role => new Claim(ClaimTypes.Role, role)));
            user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        }

        return new DapperMaticOperation
        {
            User = user,
            Operation = operation,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
        };
    }
}
