// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;
using MJCZone.DapperMatic.DataAnnotations;

namespace MJCZone.DapperMatic.Tests;

// Models with relations, for the translation tests and the database tests: customers, their orders, the orders'
// lines and products, products' tags through a link table, and attribute rows (EAV) of documents keyed by UUID.

[DmTable(tableName: "rel_customers")]
public class RelCustomer
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmColumn("name", length: 100)]
    public string Name { get; set; } = string.Empty;

    [DmColumn("country", length: 50, isNullable: true)]
    public string? Country { get; set; }

    [DmColumn("is_active")]
    public bool IsActive { get; set; } = true;

    [DmRelation]
    public List<RelOrder>? Orders { get; set; }
}

[DmTable(tableName: "rel_products")]
public class RelProduct
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmColumn("name", length: 100)]
    public string Name { get; set; } = string.Empty;

    [DmColumn("origin", length: 50, isNullable: true)]
    public string? Origin { get; set; }

    [DmColumn("price", precision: 10, scale: 2)]
    public decimal Price { get; set; }

    [DmRelation(through: typeof(RelProductTag))]
    public List<RelTag>? Tags { get; set; }

    [DmRelation]
    public List<RelLine>? Lines { get; set; }
}

[DmTable(tableName: "rel_orders")]
public class RelOrder
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmColumn("customer_id", isNullable: true, isForeignKey: true, referencedTableName: "rel_customers", referencedColumnName: "id")]
    public int? CustomerId { get; set; }

    [DmColumn("status", length: 20)]
    public string Status { get; set; } = "open";

    [DmColumn("ship_country", length: 50, isNullable: true)]
    public string? ShipCountry { get; set; }

    [DmRelation]
    public RelCustomer? Customer { get; set; }

    [DmRelation]
    public List<RelLine>? Lines { get; set; }
}

[DmTable(tableName: "rel_lines")]
public class RelLine
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmColumn("order_id", isForeignKey: true, referencedTableName: "rel_orders", referencedColumnName: "id")]
    public int OrderId { get; set; }

    [DmColumn("product_id", isForeignKey: true, referencedTableName: "rel_products", referencedColumnName: "id")]
    public int ProductId { get; set; }

    [DmColumn("qty")]
    public int Qty { get; set; }

    [DmRelation]
    public RelOrder? Order { get; set; }

    [DmRelation]
    public RelProduct? Product { get; set; }
}

[DmTable(tableName: "rel_tags")]
public class RelTag
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmColumn("name", length: 50)]
    public string Name { get; set; } = string.Empty;
}

[DmTable(tableName: "rel_product_tags")]
[DmPrimaryKeyConstraint(["product_id", "tag_id"])]
public class RelProductTag
{
    [DmColumn("product_id", isForeignKey: true, referencedTableName: "rel_products", referencedColumnName: "id")]
    public int ProductId { get; set; }

    [DmColumn("tag_id", isForeignKey: true, referencedTableName: "rel_tags", referencedColumnName: "id")]
    public int TagId { get; set; }
}

// Attribute rows of many kinds of object, joined by type and UUID: no foreign key.
[DmTable(tableName: "rel_documents")]
public class RelDocument
{
    [DmColumn("id", isPrimaryKey: true)]
    public Guid Id { get; set; }

    [DmColumn("title", length: 100)]
    public string Title { get; set; } = string.Empty;

    [DmRelation(
        thisKeys: [nameof(Id)],
        otherKeys: [nameof(RelAttribute.ObjectId)],
        otherValues: [nameof(RelAttribute.ObjectType), "document"],
        condition: nameof(Live)
    )]
    public List<RelAttribute>? Attributes { get; set; }

    // One attribute row: unique by type, object and name.
    [DmRelation(
        thisKeys: [nameof(Id)],
        otherKeys: [nameof(RelAttribute.ObjectId)],
        otherValues: [nameof(RelAttribute.ObjectType), "document", nameof(RelAttribute.Name), "color"]
    )]
    public RelAttribute? Color { get; set; }

    private static Expression<Func<RelAttribute, bool>> Live => a => !a.IsDeleted;
}

[DmTable(tableName: "rel_attributes")]
[DmUniqueConstraint(columnNames: ["object_type", "object_id", "name"], constraintName: "uc_rel_attributes_object_name")]
public class RelAttribute
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmColumn("object_type", length: 20)]
    public string ObjectType { get; set; } = string.Empty;

    [DmColumn("object_id")]
    public Guid ObjectId { get; set; }

    [DmColumn("name", length: 50)]
    public string Name { get; set; } = string.Empty;

    [DmColumn("value", length: 100, isNullable: true)]
    public string? Value { get; set; }

    [DmColumn("is_deleted")]
    public bool IsDeleted { get; set; }
}

// A model without relation properties, for WhereExists.
[DmTable(tableName: "rel_notes")]
public class RelNote
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmColumn("order_id")]
    public int OrderId { get; set; }

    [DmColumn("flagged")]
    public bool Flagged { get; set; }
}
