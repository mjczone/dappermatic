// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

#pragma warning disable SA1402 // The row-writing requests belong together.
#pragma warning disable SA1649

using System.Text.Json;

namespace MJCZone.DapperMatic.AspNetCore.Models.Dtos;

/// <summary>
/// Rows to insert into a table, all or none.
/// </summary>
public class RowsInsertDto
{
    /// <summary>Gets or sets the rows, as column name to value.</summary>
    public List<Dictionary<string, JsonElement>> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether to roll the write back after it runs, reporting how many rows it would
    /// have changed: a dry run.
    /// </summary>
    public bool DryRun { get; set; }
}

/// <summary>
/// Columns to set in the rows of a table that match a condition.
/// </summary>
public class RowsUpdateDto
{
    /// <summary>Gets or sets the columns and their values.</summary>
    public Dictionary<string, JsonElement> Set { get; set; } = [];

    /// <summary>Gets or sets the condition, as <c>$filter</c> writes it: <c>status eq 'open'</c>, or <c>true</c> for every row.</summary>
    public string? Filter { get; set; }

    /// <summary>Gets or sets the condition as a JSON tree, ANDed with <see cref="Filter"/>.</summary>
    public JsonElement? Where { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to roll the write back after it runs, reporting how many rows it would
    /// have changed: a dry run.
    /// </summary>
    public bool DryRun { get; set; }
}

/// <summary>
/// The rows of a table to delete: those that match a condition.
/// </summary>
public class RowsDeleteDto
{
    /// <summary>Gets or sets the condition, as <c>$filter</c> writes it: <c>status eq 'void'</c>, or <c>true</c> for every row.</summary>
    public string? Filter { get; set; }

    /// <summary>Gets or sets the condition as a JSON tree, ANDed with <see cref="Filter"/>.</summary>
    public JsonElement? Where { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to roll the write back after it runs, reporting how many rows it would
    /// have changed: a dry run.
    /// </summary>
    public bool DryRun { get; set; }
}

/// <summary>
/// Writes of rows of a datasource's tables, in order, all or none: a later write may use a value an earlier one wrote,
/// as <c>"$1.id"</c>, the <c>id</c> of the first row the first operation inserted.
/// </summary>
public class BatchDto
{
    /// <summary>Gets or sets the operations, run in order in one transaction.</summary>
    public List<BatchOperationDto> Operations { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether to roll every write back after they run: a dry run.</summary>
    public bool DryRun { get; set; }
}

/// <summary>
/// One write of a batch: an insert, an update or a delete of a table's rows, as the row endpoints take it.
/// </summary>
/// <remarks>
/// A value that is a string <c>"$n.column"</c> is the value of the column of the first row operation n (counting
/// from 1) inserted; <c>"$n[i].column"</c> is that of its row i (counting from 0). <c>"$$..."</c> is a string that
/// starts with one <c>$</c>.
/// </remarks>
public class BatchOperationDto
{
    /// <summary>Gets or sets what the operation does: <c>insert</c>, <c>update</c> or <c>delete</c>.</summary>
    public string? Op { get; set; }

    /// <summary>Gets or sets the table.</summary>
    public string? Table { get; set; }

    /// <summary>Gets or sets the table's schema, or null for the default one (or the tenant's).</summary>
    public string? Schema { get; set; }

    /// <summary>Gets or sets an insert's rows, as column name to value.</summary>
    public List<Dictionary<string, JsonElement>>? Rows { get; set; }

    /// <summary>Gets or sets an update's columns and their values.</summary>
    public Dictionary<string, JsonElement>? Set { get; set; }

    /// <summary>Gets or sets an update's or a delete's condition, as <c>$filter</c> writes it.</summary>
    public string? Filter { get; set; }

    /// <summary>Gets or sets an update's or a delete's condition as a JSON tree, ANDed with <see cref="Filter"/>.</summary>
    public JsonElement? Where { get; set; }
}
