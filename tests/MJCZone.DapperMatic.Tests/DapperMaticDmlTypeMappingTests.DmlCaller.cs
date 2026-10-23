// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Security.Claims;
using Dapper;
using MJCZone.DapperMatic.Dml;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// Typed code run as a caller, held to the policies as the web API holds requests.
/// </summary>
public abstract partial class DapperMaticDmlTypeMappingTests
{
    private static readonly DmPolicy[] CallerPolicies =
    [
        DmPolicy.Allow(DmAction.Read, "*").OnTables("cal_customers").Where("tenant_id eq @tenant.id or tenant_id eq null"),
        DmPolicy.Allow(DmAction.ReadWrite, "*")
            .OnTables("cal_invoices")
            .Where("tenant_id eq @tenant.id")
            .Preset("tenant_id", "@tenant.id")
            .ExceptColumns("secret"),
        DmPolicy.Restrict(DmAction.Write).OnTables("cal_invoices").Check("amount ge 0"),
    ];

    [Fact]
    protected virtual async Task Typed_reads_run_as_a_caller_see_what_the_policies_let_through_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await RecreateCallerTablesAsync(db);
        var tenant1 = await db.AsCallerAsync(Caller(1), CallerPolicies, "main");

        // Rows: the tenant's invoices; its customers and the global ones.
        Assert.Equal([1, 3], (await tenant1.SelectAsync<CalInvoice>()).Select(i => i.Id).Order());
        Assert.Equal([1, 3], (await tenant1.SelectAsync<CalCustomer>()).Select(c => c.Id).Order());
        Assert.Equal(2, await tenant1.CountAsync<CalInvoice>());
        Assert.Null(await tenant1.SingleByIdAsync<CalInvoice>(2));

        // A column the caller may not read comes back empty, and cannot be compared.
        Assert.All(await tenant1.SelectAsync<CalInvoice>(), i => Assert.Null(i.Secret));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tenant1.SelectAsync<CalInvoice>(i => i.Secret == "s1"));

        // Related rows are held to their own policies: invoice 3's customer is another tenant's.
        Assert.Equal([1], (await tenant1.From<CalInvoice>().Where(i => i.Customer!.Name == "Acme").ToListAsync()).Select(i => i.Id));
        Assert.Empty(await tenant1.From<CalInvoice>().Where(i => i.Customer!.Name == "Globex").ToListAsync());
        var three = await tenant1.From<CalInvoice>().Where(i => i.Id == 3).Include(i => i.Customer).SingleAsync();
        Assert.Null(three!.Customer);
        var acme = await tenant1.From<CalCustomer>().Where(c => c.Id == 1).Include(c => c.Invoices).SingleAsync();
        Assert.Equal([1], acme!.Invoices!.Select(i => i.Id));

        // With no tenant, "the caller's tenant" is no tenant: nothing of the tenants', the global rows still.
        var noTenant = await db.AsCallerAsync(Caller(null), CallerPolicies, "main");
        Assert.Empty(await noTenant.SelectAsync<CalInvoice>());
        Assert.Equal([3], (await noTenant.SelectAsync<CalCustomer>()).Select(c => c.Id));

        // A table no policy grants is refused; trusted code reads everything.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tenant1.SelectAsync<CalAudit>());
        Assert.Equal(3, await db.CountAsync<CalInvoice>());

        await DropCallerTablesAsync(db);
    }

    [Fact]
    protected virtual async Task Typed_writes_run_as_a_caller_are_held_to_the_policies_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await RecreateCallerTablesAsync(db);
        var tenant1 = await db.AsCallerAsync(Caller(1), CallerPolicies, "main");
        async Task<CalInvoice> Stored(int id) => (await db.SingleByIdAsync<CalInvoice>(id))!;

        // Inserts: the policy writes the tenant, the column the caller may not write is left out, the check holds.
        var added = new CalInvoice { TenantId = 2, CustomerId = 1, Amount = 5, Secret = "mine" };
        var id = Convert.ToInt32(await tenant1.InsertAsync(added));
        Assert.Equal((1, null), ((await Stored(id)).TenantId, (await Stored(id)).Secret));
        Assert.Equal(2, await tenant1.InsertManyAsync<CalInvoice>([new() { CustomerId = 1, Amount = 6 }, new() { CustomerId = 1, Amount = 7, TenantId = 9 }]));
        Assert.Equal(1, await tenant1.BulkInsertAsync<CalInvoice>([new() { CustomerId = 1, Amount = 8, Secret = "x" }]));
        Assert.Equal(7, await db.CountAsync<CalInvoice>());
        Assert.Equal(6, await db.CountAsync<CalInvoice>(i => i.TenantId == 1));
        Assert.Equal(3, await db.CountAsync<CalInvoice>(i => i.Secret != null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tenant1.InsertAsync(new CalInvoice { CustomerId = 1, Amount = -5 }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tenant1.BulkInsertAsync<CalInvoice>([new() { CustomerId = 1, Amount = 1 }, new() { CustomerId = 1, Amount = -1 }]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tenant1.InsertAsync(new CalCustomer { Id = 9, Name = "x" }));
        var noTenant = await db.AsCallerAsync(Caller(null), CallerPolicies, "main");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => noTenant.InsertAsync(new CalInvoice { CustomerId = 1, Amount = 1 }));
        Assert.Equal(7, await db.CountAsync<CalInvoice>());

        // Updates by row: another tenant's row is not there; the caller's own keeps the column it cannot see.
        var others = await Stored(2);
        others.Amount = 99;
        Assert.Equal(0, await tenant1.UpdateAsync(others));
        Assert.Equal(20, (await Stored(2)).Amount);
        var mine = (await tenant1.SingleByIdAsync<CalInvoice>(1))!;
        mine.Amount = 11;
        mine.TenantId = 2;
        Assert.Equal(1, await tenant1.UpdateAsync(mine));
        Assert.Equal((11, 1, "s1"), ((await Stored(1)).Amount, (await Stored(1)).TenantId, (await Stored(1)).Secret));
        mine.Amount = -1;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tenant1.UpdateAsync(mine));
        Assert.Equal(11, (await Stored(1)).Amount);

        // Updates by condition and patches: not the hidden column, not past the check, only the tenant's rows.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tenant1.UpdateAsync<CalInvoice>(i => new CalInvoice { Secret = "x" }, i => i.Id == 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tenant1.UpdateAsync<CalInvoice>(i => new CalInvoice { Amount = i.Amount - 100 }, i => true));
        Assert.Equal(11, (await Stored(1)).Amount);
        Assert.Equal(1, await tenant1.UpdateAsync<CalInvoice>(i => new CalInvoice { Amount = i.Amount + 1 }, i => i.Id == 1 || i.Id == 2));
        Assert.Equal((12, 20), ((await Stored(1)).Amount, (await Stored(2)).Amount));
        Assert.Equal(1, await tenant1.PatchAsync<CalInvoice>(new { Id = 1, Status = "sent" }));
        Assert.Equal(0, await tenant1.PatchAsync<CalInvoice>(new { Id = 2, Status = "sent" }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tenant1.PatchAsync<CalInvoice>(new { Id = 1, Secret = "x" }));
        Assert.Equal(("sent", "paid"), ((await Stored(1)).Status, (await Stored(2)).Status));

        // Upserts: the caller's row is updated, another tenant's refused, a new one inserted.
        Assert.Equal(3, Convert.ToInt32(await tenant1.UpsertAsync(new CalInvoice { Id = 3, CustomerId = 1, Amount = 33 })));
        Assert.Equal((33, "s3"), ((await Stored(3)).Amount, (await Stored(3)).Secret));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tenant1.UpsertAsync(new CalInvoice { Id = 2, CustomerId = 1, Amount = 1 }));
        var upserted = new CalInvoice { CustomerId = 1, Amount = 40 };
        await tenant1.UpsertAsync(upserted);
        Assert.Equal(1, (await Stored(upserted.Id)).TenantId);

        // Deletes: never another tenant's row, never by the hidden column.
        Assert.Equal(0, await tenant1.DeleteByIdAsync<CalInvoice>(2));
        Assert.Equal(0, await tenant1.DeleteAsync<CalInvoice>(i => i.Id == 2));
        Assert.Equal(0, await tenant1.DeleteAsync(others));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tenant1.DeleteAsync<CalInvoice>(i => i.Secret == "s2"));
        Assert.Equal(1, await tenant1.DeleteByIdAsync<CalInvoice>(1));
        Assert.Null(await db.SingleByIdAsync<CalInvoice>(1));
        Assert.NotNull(await db.SingleByIdAsync<CalInvoice>(2));

        await DropCallerTablesAsync(db);
    }

    [Fact]
    protected virtual async Task Typed_writes_run_as_a_caller_tell_and_change_nothing_unseen_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await RecreateCallerTablesAsync(db);
        async Task<CalInvoice> Stored(int id) => (await db.SingleByIdAsync<CalInvoice>(id))!;
        var tenant1 = await db.AsCallerAsync(Caller(1), CallerPolicies, "main");

        // Inserting unless a row exists asks first whether the caller may insert at all: whether another tenant's
        // customer 2 exists is not told to a caller who may only read customers.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tenant1.InsertIfAbsentAsync(new CalCustomer { Id = 2, Name = "x" }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tenant1.InsertIfAbsentAsync(new CalAudit { Id = 1, What = "x" }));

        // A check that is unknown (NULL) refuses a row in bulk as it does one by one.
        var checkedSecret = await db.AsCallerAsync(
            Caller(1),
            [
                DmPolicy.Allow(DmAction.ReadWrite, "*").OnTables("cal_invoices").Where("tenant_id eq @tenant.id").Preset("tenant_id", "@tenant.id"),
                DmPolicy.Restrict(DmAction.Create).OnTables("cal_invoices").Check("secret eq 'ok'"),
            ],
            "main"
        );
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => checkedSecret.InsertAsync(new CalInvoice { CustomerId = 1, Amount = 1 }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => checkedSecret.BulkInsertAsync<CalInvoice>([new() { CustomerId = 1, Amount = 1 }]));
        Assert.Equal(3, await db.CountAsync<CalInvoice>());

        // A column the caller may write but not read comes back empty, and a whole row written back keeps it.
        var writeOnly = await db.AsCallerAsync(
            Caller(1),
            [
                DmPolicy.Allow(DmAction.ReadWrite, "*").OnTables("cal_invoices").Where("tenant_id eq @tenant.id"),
                DmPolicy.Restrict(DmAction.Read).OnTables("cal_invoices").ExceptColumns("secret"),
            ],
            "main"
        );
        var one = (await writeOnly.SingleByIdAsync<CalInvoice>(1))!;
        Assert.Null(one.Secret);
        one.Amount = 12;
        Assert.Equal(1, await writeOnly.UpdateAsync(one));
        Assert.Equal((12, "s1"), ((await Stored(1)).Amount, (await Stored(1)).Secret));

        // An update that would break the check changes nothing, even in the application's transaction it goes on to
        // commit: invoice 3 would pass, invoice 1 would not.
        using (var tx = db.BeginTransaction())
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tenant1.UpdateAsync<CalInvoice>(i => new CalInvoice { Amount = i.Amount - 15 }, i => true, tx));
            tx.Commit();
        }

        Assert.Equal((12, 30), ((await Stored(1)).Amount, (await Stored(3)).Amount));

        // A tenant kept in a schema of its own is held to it (on providers with schemas: the others refuse InSchema).
        if (db.SupportsSchemas())
        {
            var kept = Caller(1);
            kept.Tenant!.SchemaName = "tenant_1";
            var inSchema = await db.AsCallerAsync(kept, CallerPolicies, "main");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => inSchema.InSchema("tenant_2").SelectAsync<CalInvoice>());
        }

        await DropCallerTablesAsync(db);
    }

    private static DmAccessContext Caller(int? tenant) =>
        new(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ann")], "Test"))) { Tenant = tenant is null ? null : new DmTenant(tenant.Value) };

    private static async Task RecreateCallerTablesAsync(IDbConnection db)
    {
        await DropCallerTablesAsync(db);
        foreach (var type in new[] { typeof(CalCustomer), typeof(CalInvoice), typeof(CalAudit) })
        {
            await db.CreateTableIfNotExistsAsync(DmTableFactory.GetTable(type));
        }

        await db.InsertManyAsync<CalCustomer>([new() { Id = 1, TenantId = 1, Name = "Acme" }, new() { Id = 2, TenantId = 2, Name = "Globex" }, new() { Id = 3, Name = "Shared" }]);
        await db.InsertAsync(new CalInvoice { TenantId = 1, CustomerId = 1, Amount = 10, Status = "paid", Secret = "s1" });
        await db.InsertAsync(new CalInvoice { TenantId = 2, CustomerId = 2, Amount = 20, Status = "paid", Secret = "s2" });
        await db.InsertAsync(new CalInvoice { TenantId = 1, CustomerId = 2, Amount = 30, Status = "draft", Secret = "s3" });
    }

    private static async Task DropCallerTablesAsync(IDbConnection db)
    {
        foreach (var type in new[] { typeof(CalAudit), typeof(CalInvoice), typeof(CalCustomer) })
        {
            var table = DmTableFactory.GetTable(type);
            await db.DropTableIfExistsAsync(table.SchemaName, table.TableName);
        }
    }
}
