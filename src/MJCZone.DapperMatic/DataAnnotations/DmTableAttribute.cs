// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.DataAnnotations;

/// <summary>
/// Attribute to define the table name and schema for a class.
/// </summary>
/// <example>
/// [DmTable("dbo", "MyTable")]
/// public class MyTable { }
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DmTableAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmTableAttribute"/> class.
    /// </summary>
    /// <param name="schemaName">The name of the schema.</param>
    /// <param name="tableName">The name of the table.</param>
    public DmTableAttribute(string? schemaName = null, string? tableName = null)
    {
        SchemaName = schemaName;
        TableName = tableName;
    }

    /// <summary>
    /// Gets the name of the schema.
    /// </summary>
    public string? SchemaName { get; }

    /// <summary>
    /// Gets the name of the table.
    /// </summary>
    public string? TableName { get; }
}
