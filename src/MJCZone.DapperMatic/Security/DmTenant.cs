// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Security;

/// <summary>
/// The tenant a request acts for, and what is known about it. Nothing here assumes a tenant model: a tenant may be a
/// key on rows, a schema, a database, a node of a tree, or any mix; policies and session values read what they need.
/// </summary>
public sealed class DmTenant
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmTenant"/> class.
    /// </summary>
    /// <param name="id">The tenant's id, as the application knows it: a string, a number, a GUID.</param>
    public DmTenant(object id)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
    }

    /// <summary>Gets the tenant's id.</summary>
    public object Id { get; }

    /// <summary>
    /// Gets or sets the datasource the tenant's data is in, or null for none in particular. When set, a request naming
    /// another datasource is refused.
    /// </summary>
    public string? DatasourceId { get; set; }

    /// <summary>
    /// Gets or sets the schema the tenant's tables are in, or null for none in particular. When set, a request naming
    /// another schema is refused, and one naming none uses it.
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Gets values known about the tenant, which policies and session values read as <c>@tenant.name</c>: its path in a
    /// tree, the ids below it, its region. A value may also be a function computing it when first read.
    /// </summary>
    public Dictionary<string, object?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string ToString() => Convert.ToString(Id, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
}
