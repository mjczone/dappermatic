// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// How policies combine, as PostgreSQL's do: grants ORed, fences ANDed, nothing without a grant.
/// </summary>
public class DmPoliciesTests
{
    private static readonly DmResource Orders = new("ds", "sales", "orders", ["id", "tenant_id", "total", "notes", "cost"]);

    [Fact]
    public void Nothing_is_allowed_without_a_grant()
    {
        var fence = DmPolicy.Restrict(DmAction.Read).Where("total gt 0");
        var rules = DmPolicies.Evaluate([fence], Caller(), Orders, DmAction.Read);
        Assert.False(rules.Allowed);
        Assert.True(rules.Targeted);

        rules = DmPolicies.Evaluate([DmPolicy.Allow(DmAction.Read, "*").OnTables("customers")], Caller(), Orders, DmAction.Read);
        Assert.False(rules.Allowed);
        Assert.False(rules.Targeted);
    }

    [Fact]
    public void Grants_are_ORed_and_fences_ANDed_and_a_check_defaults_to_the_filter()
    {
        DmPolicy[] policies =
        [
            DmPolicy.Allow(DmAction.ReadWrite, "staff").Where("tenant_id eq @tenant.id"),
            DmPolicy.Allow(DmAction.Read, "staff").Where("tenant_id eq null"),
            DmPolicy.Restrict(DmAction.ReadWrite).Where("total ge 0"),
            DmPolicy.Restrict(DmAction.ReadWrite, "intern").Where("total lt 100"),
        ];

        var read = DmPolicies.Evaluate(policies, Caller("staff"), Orders, DmAction.Read);
        Assert.True(read.Allowed);
        Assert.Equal("((tenant_id eq @tenant.id) or (tenant_id eq null)) and (total ge 0)", read.Filter);

        var update = DmPolicies.Evaluate(policies, Caller("staff", "intern"), Orders, DmAction.Update);
        Assert.Equal("(tenant_id eq @tenant.id) and (total ge 0) and (total lt 100)", update.Filter);
        Assert.Equal(update.Filter, update.Check);

        // A grant with no filter is every row: the other grants' filters do not narrow it.
        var all = DmPolicies.Evaluate([.. policies, DmPolicy.Allow(DmAction.Read, "auditor")], Caller("staff", "auditor"), Orders, DmAction.Read);
        Assert.Equal("(total ge 0)", all.Filter);

        Assert.False(DmPolicies.Evaluate(policies, Caller(), Orders, DmAction.Read).Allowed);
    }

    [Fact]
    public void A_blank_check_is_the_filter_and_true_checks_nothing()
    {
        var blank = DmPolicy.Allow(DmAction.Create, "staff").Where("tenant_id eq @tenant.id").Check(" ");
        Assert.Equal("(tenant_id eq @tenant.id)", DmPolicies.Evaluate([blank], Caller("staff"), Orders, DmAction.Create).Check);

        var none = DmPolicy.Allow(DmAction.Create, "staff").Where("tenant_id eq @tenant.id").Check("true");
        Assert.Equal("(true)", DmPolicies.Evaluate([none], Caller("staff"), Orders, DmAction.Create).Check);
    }

    [Fact]
    public void Managing_a_datasource_needs_a_grant_for_all_of_it()
    {
        var datasource = new DmResource("ds", DmResource.Any, null);
        Assert.False(DmPolicies.Evaluate([DmPolicy.Allow(DmAction.ManageDatasources, "dba").InSchemas("t1")], Caller("dba"), datasource, DmAction.ManageDatasources).Allowed);
        Assert.False(DmPolicies.Evaluate([DmPolicy.Allow(DmAction.ManageDatasources, "dba").ExceptSchemas("dbo")], Caller("dba"), datasource, DmAction.ManageDatasources).Allowed);
        Assert.True(DmPolicies.Evaluate([DmPolicy.Allow(DmAction.ManageDatasources, "dba").InSchemas("*")], Caller("dba"), datasource, DmAction.ManageDatasources).Allowed);
        Assert.True(DmPolicies.Evaluate([DmPolicy.Allow(DmAction.ManageDatasources, "dba").OnDatasources("ds")], Caller("dba"), datasource, DmAction.ManageDatasources).Allowed);

        // Writing a view's SQL is the same: it can read the whole datasource.
        Assert.False(DmPolicies.Evaluate([DmPolicy.Allow(DmAction.DefineViews, "dba").OnTables("dev_*")], Caller("dba"), datasource, DmAction.DefineViews).Allowed);
        Assert.True(DmPolicies.Evaluate([DmPolicy.Allow(DmAction.All, "dba")], Caller("dba"), datasource, DmAction.DefineViews).Allowed);

        // A datasource added without an id is one no list names.
        var unnamed = new DmResource(string.Empty, DmResource.Any, null);
        Assert.False(DmPolicies.Evaluate([DmPolicy.Allow(DmAction.ManageDatasources, "dba").OnDatasources("ds")], Caller("dba"), unnamed, DmAction.ManageDatasources).Allowed);
        Assert.True(DmPolicies.Evaluate([DmPolicy.Allow(DmAction.ManageDatasources, "dba")], Caller("dba"), unnamed, DmAction.ManageDatasources).Allowed);
    }

    [Fact]
    public void Columns_are_what_any_grant_allows_less_what_any_policy_excludes_within_every_fence()
    {
        DmPolicy[] policies =
        [
            DmPolicy.Allow(DmAction.Read, "*").OnlyColumns("id", "total", "notes"),
            DmPolicy.Allow(DmAction.Read, "finance").OnlyColumns("id", "cost"),
            DmPolicy.Restrict(DmAction.Read).ExceptColumns("notes"),
        ];

        Assert.Equal(["id", "total"], Sorted(DmPolicies.Evaluate(policies, Caller(), Orders, DmAction.Read).Columns));
        Assert.Equal(["cost", "id", "total"], Sorted(DmPolicies.Evaluate(policies, Caller("finance"), Orders, DmAction.Read).Columns));

        // An exclusion with every column granted leaves the table's other columns.
        var excluded = DmPolicies.Evaluate([DmPolicy.Allow(DmAction.Read, "*").ExceptColumns("cost")], Caller(), Orders, DmAction.Read);
        Assert.Equal(["id", "notes", "tenant_id", "total"], Sorted(excluded.Columns));

        // A fence's own list holds whatever the grants allow.
        var fenced = DmPolicies.Evaluate([DmPolicy.Allow(DmAction.Read, "*"), DmPolicy.Restrict(DmAction.Read).OnlyColumns("id")], Caller(), Orders, DmAction.Read);
        Assert.Equal(["id"], Sorted(fenced.Columns));
    }

    [Fact]
    public void Policies_cover_by_target_role_claim_and_predicate()
    {
        var tenantRows = DmPolicy.Allow(DmAction.Read, "*").OnTablesWithColumns("tenant_id").ExceptSchemas("platform");
        Assert.True(DmPolicies.Evaluate([tenantRows], Caller(), Orders, DmAction.Read).Allowed);
        Assert.False(DmPolicies.Evaluate([tenantRows], Caller(), Orders with { Columns = ["id"] }, DmAction.Read).Allowed);
        Assert.False(DmPolicies.Evaluate([tenantRows], Caller(), Orders with { SchemaName = "platform" }, DmAction.Read).Allowed);

        // Columns a table must have narrow row actions only, and need the table's columns.
        Assert.False(DmPolicies.Evaluate([tenantRows], Caller(), Orders with { Columns = null }, DmAction.Read).Allowed);

        var claim = DmPolicy.Allow(DmAction.Read).WithClaim("plan", "pro");
        Assert.True(DmPolicies.Evaluate([claim], Claims(new Claim("plan", "pro")), Orders, DmAction.Read).Allowed);
        Assert.False(DmPolicies.Evaluate([claim], Claims(new Claim("plan", "free")), Orders, DmAction.Read).Allowed);

        var predicate = DmPolicy.Allow(DmAction.Read).When(c => c.Tenant?.SchemaName == "sales");
        Assert.False(DmPolicies.Evaluate([predicate], Caller(), Orders, DmAction.Read).Allowed);
        var inSales = new DmAccessContext(Caller().User) { Tenant = new DmTenant("acme") { SchemaName = "sales" } };
        Assert.True(DmPolicies.Evaluate([predicate], inSales, Orders, DmAction.Read).Allowed);

        // anonymous and authenticated are roles every caller has one of; patterns match names.
        var anonymous = DmPolicy.Allow(DmAction.Read, "anonymous").OnDatasources("d?").InSchemas("sal*").OnTables("ord*");
        Assert.True(DmPolicies.Evaluate([anonymous], Caller(), Orders, DmAction.Read).Allowed);
        Assert.False(DmPolicies.Evaluate([anonymous], Caller("staff"), Orders, DmAction.Read).Allowed);
    }

    [Fact]
    public void Listings_are_about_every_datasource_or_schema()
    {
        var policy = DmPolicy.Allow(DmAction.ReadSchema, "*").OnDatasources("tenant-*").InSchemas("app");
        Assert.True(DmPolicies.Evaluate([policy], Caller(), new DmResource(DmResource.Any, DmResource.Any, null), DmAction.ReadSchema).Allowed);
        Assert.True(DmPolicies.Evaluate([policy], Caller(), new DmResource("tenant-a", DmResource.Any, null), DmAction.ReadSchema).Allowed);
        Assert.False(DmPolicies.Evaluate([policy], Caller(), new DmResource("billing", DmResource.Any, null), DmAction.ReadSchema).Allowed);
        Assert.False(DmPolicies.Evaluate([policy], Caller(), new DmResource("tenant-a", "hr", null), DmAction.ReadSchema).Allowed);

        // The default schema of a request naming none is "_".
        var defaults = DmPolicy.Allow(DmAction.ReadSchema, "*").InSchemas("_");
        Assert.True(DmPolicies.Evaluate([defaults], Caller(), new DmResource("ds", null, "orders"), DmAction.ReadSchema).Allowed);
        Assert.False(DmPolicies.Evaluate([defaults], Caller(), new DmResource("ds", "sales", "orders"), DmAction.ReadSchema).Allowed);
    }

    [Fact]
    public void A_fences_presets_win_over_a_grants()
    {
        DmPolicy[] policies =
        [
            DmPolicy.Allow(DmAction.Create, "*").Preset("source", "api").Preset("tenant_id", "@claims.tenant"),
            DmPolicy.Restrict(DmAction.Create).Preset("tenant_id", "@tenant.id"),
        ];
        var rules = DmPolicies.Evaluate(policies, Caller(), Orders, DmAction.Create);
        Assert.Equal("api", rules.Presets["source"]);
        Assert.Equal("@tenant.id", rules.Presets["tenant_id"]);
    }

    [Theory]
    [InlineData("tenant_id eq @tenant.id and total gt 0", null)]
    [InlineData("tenant_id in @tenant.below or tenant_id eq null", null)]
    [InlineData("customer/country eq 'FR'", "another table")]
    [InlineData("lines/any(l: l/total gt 0)", "related rows")]
    [InlineData("tenant_id eq", "")]
    public void Policies_are_validated(string filter, string? refusal)
    {
        var policy = DmPolicy.Allow(DmAction.Read, "*").Named("p").Where(filter);
        if (refusal is null)
        {
            DmPolicies.Validate(policy);
            return;
        }

        var ex = Assert.ThrowsAny<ArgumentException>(() => DmPolicies.Validate(policy));
        Assert.Contains(refusal, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_grant_must_name_who_it_is_for()
    {
        Assert.Throws<ArgumentException>(() => DmPolicies.Validate(new DmPolicy { Actions = DmAction.Read }));
        Assert.Throws<ArgumentException>(() => DmPolicies.Validate(DmPolicy.Allow(DmAction.None, "*")));
        DmPolicies.Validate(DmPolicy.Restrict(DmAction.Read).Where("total ge 0"));
    }

    [Fact]
    public void Functions_are_run_only_by_grants_naming_them_or_confined_to_nothing()
    {
        DmAccessRules Run(string function, params DmPolicy[] policies) =>
            DmPolicies.Evaluate(policies, Caller("staff"), new DmResource("ds", DmResource.Any, null) { FunctionName = function }, DmAction.Execute);

        Assert.True(Run("report", DmPolicy.Allow(DmAction.Execute, "staff").OnFunctions("rep*")).Allowed);
        Assert.False(Run("purge", DmPolicy.Allow(DmAction.Execute, "staff").OnFunctions("rep*")).Allowed);
        Assert.True(Run("purge", DmPolicy.Allow(DmAction.All, "staff")).Allowed);
        Assert.True(Run("purge", DmPolicy.Allow(DmAction.All, "staff").OnDatasources("ds")).Allowed);

        // Confined to schemas or tables, a grant does not reach a function's SQL, which reads and writes what it likes.
        Assert.False(Run("purge", DmPolicy.Allow(DmAction.All, "staff").OnTables("orders")).Allowed);
        Assert.False(Run("purge", DmPolicy.Allow(DmAction.All, "staff").InSchemas("sales")).Allowed);
        Assert.False(Run("purge", DmPolicy.Allow(DmAction.All, "staff").OnTablesWithColumns("tenant_id")).Allowed);

        // A policy naming functions grants running them and nothing else.
        Assert.False(DmPolicies.Evaluate([DmPolicy.Allow(DmAction.Execute, "staff").OnFunctions("report")], Caller("staff"), Orders, DmAction.Read).Allowed);
        Assert.Throws<ArgumentException>(() => DmPolicies.Validate(DmPolicy.Allow(DmAction.Read | DmAction.Execute, "staff").OnFunctions("report")));
        DmPolicies.Validate(DmPolicy.Allow(DmAction.Execute, "staff").OnFunctions("report"));
    }

    private static DmAccessContext Caller(params string[] roles) =>
        roles.Length == 0
            ? new DmAccessContext()
            : new DmAccessContext(new ClaimsPrincipal(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "Test")));

    private static DmAccessContext Claims(params Claim[] claims) => new(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")));

    private static string[] Sorted(IReadOnlySet<string>? columns) => [.. (columns ?? throw new InvalidOperationException("All columns")).Order(StringComparer.Ordinal)];
}
