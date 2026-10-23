// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.DataAnnotations;

namespace MJCZone.DapperMatic.Tests;

// Models typed code reads and writes as a caller, held to policies: customers with global rows (no tenant), and
// invoices kept per tenant with a column the caller may not see.

[DmTable(tableName: "cal_customers")]
public class CalCustomer
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("tenant_id", isNullable: true)]
    public int? TenantId { get; set; }

    [DmColumn("name", length: 50)]
    public string Name { get; set; } = string.Empty;

    [DmRelation]
    public List<CalInvoice>? Invoices { get; set; }
}

[DmTable(tableName: "cal_invoices")]
public class CalInvoice
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmColumn("tenant_id", isNullable: true)]
    public int? TenantId { get; set; }

    [DmColumn("customer_id", isForeignKey: true, referencedTableName: "cal_customers", referencedColumnName: "id")]
    public int CustomerId { get; set; }

    [DmColumn("amount")]
    public int Amount { get; set; }

    [DmColumn("status", length: 20)]
    public string Status { get; set; } = "draft";

    [DmColumn("secret", length: 50, isNullable: true)]
    public string? Secret { get; set; }

    [DmRelation]
    public CalCustomer? Customer { get; set; }
}

[DmTable(tableName: "cal_audits")]
public class CalAudit
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("what", length: 50)]
    public string What { get; set; } = string.Empty;
}
