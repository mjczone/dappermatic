// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Dml;

namespace MJCZone.DapperMatic.Tests;

// Models with the columns the DML methods fill and filter on their own: a tenant key, a concurrency token, audit
// columns and a soft delete.

[DmTable(tableName: "beh_notes")]
[DmUniqueConstraint(columnNames: ["tenant_id", "slug"], constraintName: "uc_beh_notes_tenant_slug")]
public class BehNote
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmTenantKey]
    [DmColumn("tenant_id", length: 20)]
    public string TenantId { get; set; } = string.Empty;

    [DmColumn("slug", length: 50)]
    public string Slug { get; set; } = string.Empty;

    [DmColumn("title", length: 100)]
    public string Title { get; set; } = string.Empty;

    [DmConcurrencyToken]
    [DmColumn("version")]
    public int Version { get; set; }

    [DmCreatedAt]
    [DmColumn("created_at")]
    public DateTime CreatedAt { get; set; }

    [DmUpdatedAt]
    [DmColumn("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [DmCreatedBy]
    [DmColumn("created_by", length: 50, isNullable: true)]
    public string? CreatedBy { get; set; }

    [DmUpdatedBy]
    [DmColumn("updated_by", length: 50, isNullable: true)]
    public string? UpdatedBy { get; set; }

    [DmSoftDelete]
    [DmColumn("deleted_at", isNullable: true)]
    public DateTime? DeletedAt { get; set; }

    [DmRelation]
    public List<BehComment>? Comments { get; set; }
}

[DmTable(tableName: "beh_comments")]
public class BehComment
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmTenantKey]
    [DmColumn("tenant_id", length: 20)]
    public string TenantId { get; set; } = string.Empty;

    [DmColumn("note_id", isForeignKey: true, referencedTableName: "beh_notes", referencedColumnName: "id")]
    public int NoteId { get; set; }

    [DmColumn("text", length: 100)]
    public string Text { get; set; } = string.Empty;

    [DmConcurrencyToken]
    [DmColumn("stamp")]
    public Guid Stamp { get; set; }

    [DmSoftDelete]
    [DmColumn("is_deleted")]
    public bool IsDeleted { get; set; }

    [DmRelation]
    public BehNote? Note { get; set; }
}

// A token SQL Server writes itself (rowversion); other providers get new random bytes on each write.
[DmTable(tableName: "beh_ledgers")]
public class BehLedger
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmColumn("balance", precision: 12, scale: 2)]
    public decimal Balance { get; set; }

    [DmConcurrencyToken]
    [DmColumn("row_version", providerDataType: "{sqlserver:rowversion}", length: 8, isNullable: true)]
    public byte[]? RowVersion { get; set; }
}

// Unique across tenants, as an email often is: an upsert on it can find another tenant's row.
[DmTable(tableName: "beh_accounts")]
[DmUniqueConstraint(columnNames: ["email"], constraintName: "uc_beh_accounts_email")]
public class BehAccount
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmTenantKey]
    [DmColumn("tenant_id", length: 20)]
    public string TenantId { get; set; } = string.Empty;

    [DmColumn("email", length: 50)]
    public string Email { get; set; } = string.Empty;

    [DmColumn("name", length: 50)]
    public string Name { get; set; } = string.Empty;

    [DmConcurrencyToken]
    [DmColumn("version")]
    public int Version { get; set; }
}

// No columns of its own that the methods fill or filter, related to a model that has them.
[DmTable(tableName: "beh_attachments")]
public class BehAttachment
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmColumn("note_id")]
    public int NoteId { get; set; }

    [DmColumn("file", length: 50)]
    public string File { get; set; } = string.Empty;

    [DmRelation(thisKeys: [nameof(NoteId)], otherKeys: [nameof(BehNote.Id)])]
    public BehNote? Note { get; set; }
}

// The ambient values the tests give through DmDefaults, per test: AsyncLocal, so tests running at once do not share.
public static class BehAmbient
{
    static BehAmbient()
    {
        DmDefaults.TenantId = () => Tenant.Value;
        DmDefaults.User = () => User.Value;
    }

    public static AsyncLocal<object?> Tenant { get; } = new();

    public static AsyncLocal<object?> User { get; } = new();

    public static void Use(object? tenant, object? user)
    {
        Tenant.Value = tenant;
        User.Value = user;
    }
}

// A deployment with a schema per tenant: plans shared, projects and their tasks in each tenant's schema.
[DmTable(tableName: "sch_plans")]
public class SchPlan
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmColumn("name", length: 50)]
    public string Name { get; set; } = string.Empty;
}

[DmTable(tableName: "sch_projects")]
public class SchProject
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    // No foreign key: the plan is in another schema.
    [DmColumn("plan_id")]
    public int PlanId { get; set; }

    [DmColumn("name", length: 50)]
    public string Name { get; set; } = string.Empty;

    [DmRelation(thisKeys: [nameof(PlanId)], otherKeys: [nameof(SchPlan.Id)])]
    public SchPlan? Plan { get; set; }

    [DmRelation]
    public List<SchTask>? Tasks { get; set; }
}

[DmTable(tableName: "sch_tasks")]
public class SchTask
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmColumn("project_id", isForeignKey: true, referencedTableName: "sch_projects", referencedColumnName: "id")]
    public int ProjectId { get; set; }

    [DmColumn("title", length: 50)]
    public string Title { get; set; } = string.Empty;
}

// A table in every schema with a foreign key to a shared one, which a schema per tenant cannot have.
[DmTable(tableName: "sch_invoices")]
public class SchInvoice
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmColumn("plan_id", isForeignKey: true, referencedTableName: "sch_plans", referencedColumnName: "id")]
    public int PlanId { get; set; }
}

// The tenant schema the tests give through DmDefaults, per test.
public static class SchAmbient
{
    static SchAmbient() => DmDefaults.TenantSchema = () => Schema.Value;

    public static AsyncLocal<string?> Schema { get; } = new();
}
