// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using Dapper;
using MJCZone.DapperMatic.Dml;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// The columns the DML methods fill and filter on their own: soft delete, concurrency tokens, audit, tenant keys.
/// </summary>
public abstract partial class DapperMaticDmlTypeMappingTests
{
    [Fact]
    protected virtual async Task Deleted_rows_are_kept_and_left_out_of_reads_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await RecreateBehaviourTablesAsync(db);
        BehAmbient.Use(null, null);
        var t1 = db.ForTenant("t1").WithUser("ann");

        var (n1, n2, n3) = (Note("one"), Note("two"), Note("three"));
        await t1.InsertManyAsync([n2, n3]);
        await t1.InsertAsync(n1);
        n2 = (await t1.SingleAsync<BehNote>(n => n.Slug == "two"))!;
        n3 = (await t1.SingleAsync<BehNote>(n => n.Slug == "three"))!;

        // A delete sets the row's deletion time; reads leave it out, unless told otherwise.
        Assert.Equal(1, await t1.DeleteAsync(n2));
        Assert.NotNull(n2.DeletedAt);
        Assert.Equal(["one", "three"], (await t1.SelectAsync<BehNote>()).Select(n => n.Slug).Order());
        Assert.Equal(2, await t1.CountAsync<BehNote>());
        Assert.Null(await t1.SingleByIdAsync<BehNote>(n2.Id));
        Assert.NotNull(await t1.IncludeDeleted().SingleByIdAsync<BehNote>(n2.Id));
        Assert.Equal(["one", "three", "two"], (await t1.From<BehNote>().IncludeDeleted().OrderBy(n => n.Slug).ToListAsync()).Select(n => n.Slug));
        Assert.Equal(["two"], (await t1.From<BehNote>().OnlyDeleted().ToListAsync()).Select(n => n.Slug));
        Assert.Equal(0, await t1.DeleteAsync<BehNote>(n => n.Slug == "two"));

        // Related rows that are deleted are left out of relations, includes and joins too.
        await t1.InsertManyAsync<BehComment>(
            [
                new() { NoteId = n1.Id, Text = "kept" },
                new() { NoteId = n1.Id, Text = "gone" },
            ]
        );
        Assert.Equal(1, await t1.DeleteAsync<BehComment>(c => c.Text == "gone"));
        var withComments = await t1.From<BehNote>().Where(n => n.Id == n1.Id).Include(n => n.Comments).SingleAsync();
        Assert.Equal(["kept"], withComments!.Comments!.Select(c => c.Text));
        Assert.False(await t1.From<BehNote>().Where(n => n.Comments!.Any(c => c.Text == "gone")).ExistsAsync());
        Assert.True(await t1.From<BehNote>().IncludeDeleted().Where(n => n.Comments!.Any(c => c.Text == "gone")).ExistsAsync());
        Assert.Equal(1, await t1.From<BehComment>().Join<BehNote>((c, n) => n.Id == c.NoteId).CountAsync());
        Assert.Equal(["one", "three"], (await t1.From<BehNote>().Select(n => new { n.Slug, Comments = n.Comments!.Count() }).ToListAsync()).Select(n => n.Slug).Order());

        // Updates by condition skip deleted rows; a hard delete removes them.
        Assert.Equal(2, await t1.UpdateAsync<BehNote>(n => new BehNote { Title = "renamed" }, n => true));
        var untouched = (await t1.From<BehNote>().OnlyDeleted().SingleAsync())!;
        Assert.Equal(("two", string.Empty), (untouched.Slug, untouched.Title));
        Assert.Equal(1, await t1.HardDeleteAsync<BehComment>(c => c.Text == "gone"));
        Assert.Equal(1, await t1.HardDeleteByIdAsync<BehNote>(n2.Id));
        Assert.Equal(2, await t1.IncludeDeleted().CountAsync<BehNote>());
        Assert.Equal(1, await t1.DeleteByIdAsync<BehNote>(n3.Id));
        await Assert.ThrowsAsync<DmConcurrencyException>(() => t1.HardDeleteAsync(n3));
        n3 = (await t1.IncludeDeleted().SingleByIdAsync<BehNote>(n3.Id))!;
        Assert.Equal(1, await t1.HardDeleteAsync(n3));
        Assert.Equal(1, await t1.DeleteAllAsync<BehComment>());
        Assert.Equal(1, await t1.IncludeDeleted().CountAsync<BehComment>());
        Assert.Equal(1, await t1.HardDeleteAllAsync<BehComment>());
        Assert.Equal(0, await t1.IncludeDeleted().CountAsync<BehComment>());

        await DropBehaviourTablesAsync(db);
    }

    [Fact]
    protected virtual async Task Rows_are_written_only_with_the_token_they_were_read_with_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await RecreateBehaviourTablesAsync(db);
        BehAmbient.Use("t1", "ann");

        var note = Note("one");
        await db.InsertAsync(note);
        Assert.Equal(1, note.Version);

        // Two copies read; the first written wins, the second is refused.
        var first = (await db.SingleByIdAsync<BehNote>(note.Id))!;
        var second = (await db.SingleByIdAsync<BehNote>(note.Id))!;
        first.Title = "first";
        Assert.Equal(1, await db.UpdateAsync(first));
        Assert.Equal(2, first.Version);
        second.Title = "second";
        await Assert.ThrowsAsync<DmConcurrencyException>(() => db.UpdateAsync(second));
        await Assert.ThrowsAsync<DmConcurrencyException>(() => db.DeleteAsync(second));
        await Assert.ThrowsAsync<DmConcurrencyException>(() => db.HardDeleteAsync(second));
        await Assert.ThrowsAsync<DmConcurrencyException>(() => db.PatchAsync<BehNote>(new { note.Id, Version = 1, Title = "stale" }));
        Assert.Equal(1, await db.PatchAsync<BehNote>(new { note.Id, Version = 2, Title = "patched" }));
        var read = (await db.SingleByIdAsync<BehNote>(note.Id))!;
        Assert.Equal(("patched", 3), (read.Title, read.Version));

        // Writes by condition and patches without the token change it, unchecked.
        Assert.Equal(1, await db.UpdateAsync<BehNote>(n => new BehNote { Title = "bulk" }, n => n.Id == note.Id));
        Assert.Equal(1, await db.PatchAsync<BehNote>(new { note.Id, Title = "unchecked" }));
        Assert.Equal(5, (await db.SingleByIdAsync<BehNote>(note.Id))!.Version);
        await Assert.ThrowsAsync<DmConcurrencyException>(() => db.UpdateAsync(read));

        // A Guid token is new on every write.
        var comment = new BehComment { NoteId = note.Id, Text = "c" };
        await db.InsertAsync(comment);
        Assert.NotEqual(Guid.Empty, comment.Stamp);
        var stale = (await db.SingleByIdAsync<BehComment>(comment.Id))!;
        var stamp = comment.Stamp;
        comment.Text = "c2";
        Assert.Equal(1, await db.UpdateAsync(comment));
        Assert.NotEqual(stamp, comment.Stamp);
        await Assert.ThrowsAsync<DmConcurrencyException>(() => db.UpdateAsync(stale));

        // A byte[] token: SQL Server's rowversion, or new random bytes elsewhere.
        var ledger = new BehLedger { Balance = 10m };
        await db.InsertAsync(ledger);
        Assert.NotNull(ledger.RowVersion);
        var copy = (await db.SingleByIdAsync<BehLedger>(ledger.Id))!;
        Assert.Equal(ledger.RowVersion, copy.RowVersion);
        ledger.Balance = 20m;
        Assert.Equal(1, await db.UpdateAsync(ledger));
        Assert.NotEqual(copy.RowVersion, ledger.RowVersion);
        Assert.Equal(ledger.RowVersion, (await db.SingleByIdAsync<BehLedger>(ledger.Id))!.RowVersion);
        copy.Balance = 30m;
        await Assert.ThrowsAsync<DmConcurrencyException>(() => db.UpdateAsync(copy));
        Assert.Equal(20m, (await db.SingleByIdAsync<BehLedger>(ledger.Id))!.Balance);

        await DropBehaviourTablesAsync(db);
    }

    [Fact]
    protected virtual async Task Rows_are_kept_to_their_tenant_and_stamped_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await RecreateBehaviourTablesAsync(db);
        BehAmbient.Use(null, null);
        var before = DateTime.SpecifyKind(DateTime.UtcNow.AddSeconds(-5), DateTimeKind.Unspecified);
        var t1 = db.ForTenant("t1").WithUser("ann");
        var t2 = db.ForTenant("t2").WithUser("bob");

        var mine = Note("a");
        await t1.InsertAsync(mine);
        await t2.InsertManyAsync([Note("a"), Note("b")]);
        Assert.Equal(("t1", "ann", "ann"), (mine.TenantId, mine.CreatedBy, mine.UpdatedBy));
        Assert.InRange(mine.CreatedAt, before, DateTime.SpecifyKind(DateTime.UtcNow.AddSeconds(5), DateTimeKind.Unspecified));
        Assert.Equal(DateTimeKind.Unspecified, mine.CreatedAt.Kind);

        // Each tenant reads, counts, groups and writes its own rows only.
        Assert.Equal(["a"], (await t1.SelectAsync<BehNote>()).Select(n => n.Slug));
        Assert.Equal(2, await t2.CountAsync<BehNote>());
        Assert.Equal([2], await t2.From<BehNote>().GroupBy(n => n.TenantId).Select(g => g.Count()).ToListAsync());
        Assert.Equal(0, await t2.DeleteByIdAsync<BehNote>(mine.Id));
        Assert.Equal(0, await t2.PatchAsync<BehNote>(new { mine.Id, Version = 1, Title = "theirs" }).ContinueWith(t => t.IsFaulted ? 0 : t.Result));
        await Assert.ThrowsAsync<InvalidOperationException>(() => t2.UpdateAsync(mine));
        await Assert.ThrowsAsync<InvalidOperationException>(() => t2.InsertAsync(new BehNote { TenantId = "t1", Slug = "x" }));
        Assert.Equal(2, await t2.UpdateAsync<BehNote>(n => new BehNote { Title = "t2's" }, n => true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => t2.UpdateAsync<BehNote>(n => new BehNote { TenantId = "t1" }, n => true));
        Assert.Equal(string.Empty, (await t1.SingleByIdAsync<BehNote>(mine.Id))!.Title);

        // No tenant: refused, unless every tenant's rows are asked for; a tenant from DmDefaults counts.
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SelectAsync<BehNote>());
        Assert.Equal(3, await db.AllTenants().CountAsync<BehNote>());
        BehAmbient.Use("t2", "carl");
        Assert.Equal(2, await db.CountAsync<BehNote>());

        // Updates stamp the time and user, and keep when and by whom a row was inserted.
        mine.Title = "edited";
        var inserted = mine.CreatedAt;
        Assert.Equal(1, await t1.WithUser("dan").UpdateAsync(mine));
        var edited = (await t1.SingleByIdAsync<BehNote>(mine.Id))!;
        Assert.Equal(("ann", "dan", "edited"), (edited.CreatedBy, edited.UpdatedBy, edited.Title));
        Assert.True(edited.UpdatedAt >= edited.CreatedAt.AddSeconds(-1));
        Assert.Equal(inserted, edited.CreatedAt, TimeSpan.FromSeconds(1));
        await t1.WithUser("eve").UpdateAsync<BehNote>(n => new BehNote { Title = "bulk" }, n => n.Id == mine.Id);
        Assert.Equal("eve", (await t1.SingleByIdAsync<BehNote>(mine.Id))!.UpdatedBy);

        // An upsert inserts with the tenant and stamps, and updates keeping them; another tenant's row is not its.
        var upsert = new DmUpsertOptions<BehNote> { OnConflict = n => new { n.TenantId, n.Slug } };
        await t1.WithUser("fay").UpsertAsync(new BehNote { Slug = "a", Title = "upserted" }, upsert);
        var upserted = (await t1.SingleAsync<BehNote>(n => n.Slug == "a"))!;
        Assert.Equal(("upserted", "ann", "fay", "t1"), (upserted.Title, upserted.CreatedBy, upserted.UpdatedBy, upserted.TenantId));
        Assert.Equal(edited.Version + 2, upserted.Version);
        await t1.UpsertAsync(new BehNote { Slug = "new", Title = "fresh" }, upsert);
        Assert.Equal(2, await t1.CountAsync<BehNote>());
        Assert.Equal(["t2's", "t2's"], (await t2.SelectAsync<BehNote>()).Select(n => n.Title));

        // Bulk loads and bulk upserts too.
        await t1.WithUser("gus").BulkInsertAsync(Enumerable.Range(0, 3).Select(i => Note($"bulk{i}")));
        Assert.Equal(3, await t1.CountAsync<BehNote>(n => n.Slug.StartsWith("bulk") && n.CreatedBy == "gus" && n.Version == 1));
        await t1.WithUser("hal").BulkUpsertAsync([Note("bulk0"), Note("bulk9")], upsert);
        var bulk0 = (await t1.SingleAsync<BehNote>(n => n.Slug == "bulk0"))!;
        Assert.Equal(("gus", "hal", 2), (bulk0.CreatedBy, bulk0.UpdatedBy, bulk0.Version));
        Assert.Equal(6, await t1.CountAsync<BehNote>());
        Assert.Equal(2, await t2.CountAsync<BehNote>());

        await DropBehaviourTablesAsync(db);
    }

    [Fact]
    protected virtual async Task Reads_by_type_and_deletes_by_relation_keep_to_the_tenant_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await RecreateBehaviourTablesAsync(db);
        BehAmbient.Use(null, null);
        var t1 = db.ForTenant("t1");
        var t2 = db.ForTenant("t2");

        var (one, two, theirs) = (Note("one"), Note("two"), Note("theirs"));
        await t1.InsertAsync(one);
        await t1.InsertAsync(two);
        await t2.InsertAsync(theirs);
        await t1.DeleteAsync(two);

        // The Type overloads read as the generic ones do: the scope's tenant's rows, not deleted.
        Assert.Equal(["one"], (await t1.SelectAsync(typeof(BehNote))).Cast<BehNote>().Select(n => n.Slug));
        Assert.Equal(1, await t1.CountAsync(typeof(BehNote)));
        Assert.Equal("one", ((BehNote)(await t1.FirstAsync(typeof(BehNote)))!).Slug);
        Assert.False(await db.ForTenant("t3").ExistsAsync(typeof(BehNote)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.CountAsync(typeof(BehNote)));

        // A model without the columns, deleted by a condition on a related model with them: the related rows are the
        // tenant's only.
        await db.InsertManyAsync<BehAttachment>(
            [
                new() { NoteId = one.Id, File = "mine" },
                new() { NoteId = theirs.Id, File = "theirs" },
            ]
        );
        Assert.Equal(1, await t1.DeleteAsync<BehAttachment>(a => a.Note!.Title == string.Empty));
        Assert.Equal(["theirs"], (await db.SelectAsync<BehAttachment>()).Select(a => a.File));

        await DropBehaviourTablesAsync(db);
    }

    [Fact]
    protected virtual async Task Upserts_set_only_their_tenants_key_and_the_new_token_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await RecreateBehaviourTablesAsync(db);
        BehAmbient.Use(null, null);
        var t1 = db.ForTenant("t1");
        var t2 = db.ForTenant("t2");
        var byEmail = new DmUpsertOptions<BehAccount> { OnConflict = a => a.Email };

        // Another tenant's row conflicts: it is left alone, and its key is not this scope's to return.
        var theirs = new BehAccount { Email = "x@example.com", Name = "theirs" };
        await t2.InsertAsync(theirs);
        var mine = new BehAccount { Email = "x@example.com", Name = "mine" };
        Assert.Null(await t1.UpsertAsync(mine, byEmail));
        Assert.Equal(0, mine.Id);
        Assert.Equal("theirs", (await t2.SingleByIdAsync<BehAccount>(theirs.Id))!.Name);

        // The row's token is the one the upsert left, so it can be updated by key next.
        var account = new BehAccount { Email = "a@example.com", Name = "first" };
        await t1.InsertAsync(account);
        account.Name = "second";
        await t1.UpsertAsync(account, byEmail);
        Assert.Equal(2, account.Version);
        account.Name = "third";
        Assert.Equal(1, await t1.UpdateAsync(account));
        Assert.Equal(("third", 3), ((await t1.SingleByIdAsync<BehAccount>(account.Id))!.Name, account.Version));

        // On the generated key too, and with a token the database writes (SQL Server's rowversion).
        var ledger = new BehLedger { Balance = 1m };
        await db.InsertAsync(ledger);
        ledger.Balance = 2m;
        await db.UpsertAsync(ledger);
        ledger.Balance = 3m;
        Assert.Equal(1, await db.UpdateAsync(ledger));

        // SQL Server refuses OUTPUT without INTO on a table with a trigger; the token is read back without it.
        if (db.GetDbProviderType() == DbProviderType.SqlServer)
        {
            await db.ExecuteAsync("CREATE TRIGGER trg_beh_ledgers ON beh_ledgers AFTER UPDATE AS BEGIN SET NOCOUNT ON; END");
            ledger.Balance = 4m;
            Assert.Equal(1, await db.UpdateAsync(ledger));
            Assert.Equal(4m, (await db.SingleByIdAsync<BehLedger>(ledger.Id))!.Balance);
        }

        await DropBehaviourTablesAsync(db);
    }

    private static BehNote Note(string slug) => new() { Slug = slug };

    private static async Task RecreateBehaviourTablesAsync(IDbConnection db)
    {
        await DropBehaviourTablesAsync(db);
        foreach (var type in new[] { typeof(BehNote), typeof(BehComment), typeof(BehLedger), typeof(BehAccount), typeof(BehAttachment) })
        {
            await db.CreateTableIfNotExistsAsync(DmTableFactory.GetTable(type));
        }
    }

    private static async Task DropBehaviourTablesAsync(IDbConnection db)
    {
        foreach (var type in new[] { typeof(BehAttachment), typeof(BehAccount), typeof(BehLedger), typeof(BehComment), typeof(BehNote) })
        {
            var table = DmTableFactory.GetTable(type);
            await db.DropTableIfExistsAsync(table.SchemaName, table.TableName);
        }
    }
}
