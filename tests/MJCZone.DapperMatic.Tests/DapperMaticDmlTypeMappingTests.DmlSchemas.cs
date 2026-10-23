// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Dml;
using MJCZone.DapperMatic.Migrations;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// A schema per tenant: shared models in one schema, migrated once; every other model in each tenant's.
/// </summary>
public abstract partial class DapperMaticDmlTypeMappingTests
{
    // The same layout in every test class that runs this, so classes running at once agree on it.
    private const string SharedSchema = "sch_shared";

    [Fact]
    protected virtual async Task Schemas_share_some_models_and_keep_the_rest_their_own_Async()
    {
        using var db = await OpenConnectionAsync();
        DmSchemaLayout.Configure(layout => layout.SharedSchema(SharedSchema).Shared<SchPlan>());
        var migrator = new DmMigrator().AddTable<SchPlan>().AddTable<SchProject>().AddTable<SchTask>();
        IDbConnection Connect() => OpenConnectionAsync().GetAwaiter().GetResult();

        if (!db.SupportsSchemas())
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => migrator.MigrateSchemasAsync(Connect, [SharedSchema]));
            return;
        }

        string[] tenants = ["sch_t1", "sch_t2"];
        await DropSchemasAsync(db, [SharedSchema, .. tenants, "sch_t3"]);

        // Single-tenant first: the first tenant beside the shared models, in one schema.
        var first = await migrator.MigrateSchemasAsync(Connect, [SharedSchema]);
        Assert.True(first.Succeeded, Failures(first));
        Assert.True(Assert.Single(first.Schemas).IsShared);
        foreach (var table in new[] { "sch_plans", "sch_projects", "sch_tasks" })
        {
            Assert.True(await db.DoesTableExistAsync(SharedSchema, table));
        }

        // Tenants onboarded later get the per-tenant models only; the shared schema's run finds nothing to do.
        var second = await migrator.MigrateSchemasAsync(Connect, [SharedSchema, .. tenants]);
        Assert.True(second.Succeeded, Failures(second));
        Assert.Equal([SharedSchema, .. tenants], second.Schemas.Select(s => s.SchemaName));
        Assert.All(second.Schemas[0].Result!.Tables, t => Assert.Equal(DmTableMigrationOutcome.Unchanged, t.Outcome));
        foreach (var tenant in tenants)
        {
            Assert.True(await db.DoesTableExistAsync(tenant, "sch_projects"));
            Assert.True(await db.DoesTableExistAsync(tenant, "sch_tasks"));
            Assert.False(await db.DoesTableExistAsync(tenant, "sch_plans"));
        }

        // A tenant's scope reads and writes its own schema, and the shared models in theirs.
        var plan = new SchPlan { Name = "pro" };
        await db.InSchema(tenants[0]).InsertAsync(plan);
        Assert.Equal(1, await db.InSchema(SharedSchema).CountAsync<SchPlan>());
        var project = new SchProject { PlanId = plan.Id, Name = "apollo" };
        await db.InSchema(tenants[0]).InsertAsync(project);
        await db.InSchema(tenants[0]).InsertAsync(new SchTask { ProjectId = project.Id, Title = "launch" });
        Assert.Equal(0, await db.InSchema(tenants[1]).CountAsync<SchProject>());
        Assert.Equal(0, await db.InSchema(SharedSchema).CountAsync<SchProject>());

        // Relations from a tenant's rows to shared ones join across the schemas.
        var pro = await db.InSchema(tenants[0]).From<SchProject>().Where(p => p.Plan!.Name == "pro").Include(p => p.Plan).Include(p => p.Tasks).SingleAsync();
        Assert.Equal(("apollo", "pro", "launch"), (pro!.Name, pro.Plan!.Name, Assert.Single(pro.Tasks!).Title));

        // The tenant schema from DmDefaults, as a request would give it.
        SchAmbient.Schema.Value = tenants[0];
        Assert.Equal(1, await db.CountAsync<SchProject>());
        Assert.Equal(1, await db.CountAsync<SchPlan>());
        SchAmbient.Schema.Value = null;

        // A schema that fails does not stop the others, and is migrated again next time.
        var calls = 0;
        IDbConnection Flaky() => Interlocked.Increment(ref calls) == 3 ? throw new InvalidOperationException("down") : Connect();
        var third = await migrator.MigrateSchemasAsync(Flaky, [SharedSchema, "sch_t3", .. tenants], maxParallelism: 1);
        Assert.False(third.Succeeded);
        Assert.Equal(["sch_t3"], third.Failed.Select(f => f.SchemaName));
        var fourth = await migrator.MigrateSchemasAsync(Connect, ["sch_t3"]);
        Assert.True(fourth.Succeeded, Failures(fourth));
        Assert.True(await db.DoesTableExistAsync("sch_t3", "sch_projects"));

        // A per-tenant model cannot have a foreign key to a shared one.
        await Assert.ThrowsAsync<InvalidOperationException>(() => new DmMigrator().AddTable<SchPlan>().AddTable<SchInvoice>().MigrateSchemasAsync(Connect, tenants));

        await DropSchemasAsync(db, [SharedSchema, .. tenants, "sch_t3"]);
    }

    // SQL Server runs in different schemas deadlocked on its catalog on most runs, each scanning catalog rows the
    // others had changed, until runs read without shared locks.
    [Fact]
    protected virtual async Task Schemas_migrate_in_parallel_without_deadlocks_Async()
    {
        using var db = await OpenConnectionAsync();
        if (!db.SupportsSchemas())
        {
            Output.WriteLine("This test requires a database that supports schemas.");
            return;
        }

        DmSchemaLayout.Configure(layout => layout.SharedSchema(SharedSchema).Shared<SchPlan>());
        var retries = 0;
        var options = new DmMigrationOptions
        {
            Log = e =>
            {
                if (e.Kind == DmMigrationEventKind.Retrying)
                {
                    Interlocked.Increment(ref retries);
                }
            },
        };
        var migrator = new DmMigrator(options).AddTable<SchPlan>().AddTable<SchProject>().AddTable<SchTask>();
        IDbConnection Connect() => OpenConnectionAsync().GetAwaiter().GetResult();
        string[] tenants = [.. Enumerable.Range(1, 6).Select(i => $"sch_p{i}")];
        await DropSchemasAsync(db, [SharedSchema, .. tenants]);

        var result = await migrator.MigrateSchemasAsync(Connect, [SharedSchema, .. tenants], maxParallelism: tenants.Length);
        Assert.True(result.Succeeded, Failures(result));
        Assert.Equal(0, retries);
        foreach (var tenant in tenants)
        {
            Assert.True(await db.DoesTableExistAsync(tenant, "sch_tasks"));
        }

        await DropSchemasAsync(db, [SharedSchema, .. tenants]);
    }

    private static string Failures(DmSchemaMigrationResult result) =>
        string.Join("; ", result.Failed.Select(f => $"{f.SchemaName}: {f.Exception}"));

    private static async Task DropSchemasAsync(IDbConnection db, IEnumerable<string> schemas)
    {
        foreach (var schema in schemas)
        {
            foreach (var table in new[] { "sch_tasks", "sch_projects", "sch_plans", "dm_table_state" })
            {
                await db.DropTableIfExistsAsync(schema, table);
            }

            await db.DropSchemaIfExistsAsync(schema);
        }
    }
}
