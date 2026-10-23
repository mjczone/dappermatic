// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.DataAnnotations;

/// <summary>
/// Marks the property that says a row is deleted: a <c>bool</c> (true when deleted) or a nullable <c>DateTime</c>
/// or <c>DateTimeOffset</c> (when it was deleted). The DML methods' deletes then set it instead of removing the row,
/// and every read leaves deleted rows out unless the query says <c>IncludeDeleted()</c> or <c>OnlyDeleted()</c>;
/// <c>HardDeleteAsync</c> removes rows.
/// </summary>
/// <example>
/// <code>
/// [DmSoftDelete]
/// public DateTimeOffset? DeletedAt { get; set; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DmSoftDeleteAttribute : Attribute { }
