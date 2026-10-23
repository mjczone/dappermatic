// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models;

/// <summary>
/// A relation query requests may follow from a table: in conditions (<c>customer/country eq 'FR'</c>,
/// <c>lines/any(l: l/qty gt 5)</c>), orderings and <c>$expand</c>. A request can follow only the relations declared:
/// none is inferred from the database's foreign keys.
/// </summary>
/// <example>
/// <code>
/// options.Relations.Add(new DapperMaticRelation { TableName = "orders", Name = "customer", OtherTableName = "customers", Columns = ["customer_id"], OtherColumns = ["id"] });
/// options.Relations.Add(new DapperMaticRelation { TableName = "orders", Name = "lines", OtherTableName = "order_lines", Columns = ["id"], OtherColumns = ["order_id"], Many = true });
/// </code>
/// </example>
public sealed class DapperMaticRelation
{
    /// <summary>Gets or sets the datasource it applies to, or null for every datasource.</summary>
    public string? DatasourceId { get; set; }

    /// <summary>Gets or sets the schema of the table it is a relation of, or null for any.</summary>
    public string? SchemaName { get; set; }

    /// <summary>Gets or sets the table it is a relation of.</summary>
    public required string TableName { get; set; }

    /// <summary>Gets or sets the name requests use for it.</summary>
    public required string Name { get; set; }

    /// <summary>Gets or sets the related table's schema, or null for the table's own.</summary>
    public string? OtherSchemaName { get; set; }

    /// <summary>Gets or sets the related table.</summary>
    public required string OtherTableName { get; set; }

    /// <summary>Gets or sets the table's key columns, in pair order with <see cref="OtherColumns"/>.</summary>
    public required string[] Columns { get; set; }

    /// <summary>Gets or sets the related table's key columns.</summary>
    public required string[] OtherColumns { get; set; }

    /// <summary>Gets or sets a value indicating whether a row relates to many rows of the related table.</summary>
    public bool Many { get; set; }
}
