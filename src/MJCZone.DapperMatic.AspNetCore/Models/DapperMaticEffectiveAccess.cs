// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

#pragma warning disable SA1402 // The effective permissions belong together.

namespace MJCZone.DapperMatic.AspNetCore.Models;

/// <summary>
/// What the policies let the caller do to a datasource, schema, table or view.
/// </summary>
public sealed class DapperMaticEffectiveAccess
{
    /// <summary>Gets or sets the datasource, or null for every one.</summary>
    public string? DatasourceId { get; set; }

    /// <summary>Gets or sets the schema, or null for the default one.</summary>
    public string? SchemaName { get; set; }

    /// <summary>Gets or sets the table or view, or null for the datasource or schema as a whole.</summary>
    public string? TableName { get; set; }

    /// <summary>Gets the rules for each action, by its name.</summary>
    public Dictionary<string, DapperMaticEffectiveAction> Actions { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// What the policies let the caller do for one action. The conditions and the policies' names are shown to a caller
/// who may manage access to the resource.
/// </summary>
public sealed class DapperMaticEffectiveAction
{
    /// <summary>Gets or sets a value indicating whether the action is allowed.</summary>
    public bool Allowed { get; set; }

    /// <summary>Gets or sets the columns the caller may use, or null for all.</summary>
    public IReadOnlyList<string>? Columns { get; set; }

    /// <summary>Gets or sets the condition on the rows, or null.</summary>
    public string? Filter { get; set; }

    /// <summary>Gets or sets the condition rows written must satisfy, or null.</summary>
    public string? Check { get; set; }

    /// <summary>Gets or sets the values the policies write, by column, or null.</summary>
    public IReadOnlyDictionary<string, string?>? Presets { get; set; }

    /// <summary>Gets or sets the names of the policies that apply, or null.</summary>
    public IReadOnlyList<string>? Policies { get; set; }
}
