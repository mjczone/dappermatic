// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Represents a check constraint in a database.
/// </summary>
[Serializable]
public class DmCheckConstraint : DmConstraint
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmCheckConstraint"/> class.
    /// Used for deserialization.
    /// </summary>
    public DmCheckConstraint()
        : base(string.Empty) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmCheckConstraint"/> class.
    /// Used when schema name and table name are not necessary as when creating a table.
    /// </summary>
    /// <param name="columnName">The column name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="expression">The expression.</param>
    [SetsRequiredMembers]
    public DmCheckConstraint(string? columnName, string constraintName, string expression)
        : this(null, string.Empty, columnName, constraintName, expression) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmCheckConstraint"/> class.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="expression">The expression.</param>
    [SetsRequiredMembers]
    public DmCheckConstraint(
        string? schemaName,
        string tableName,
        string? columnName,
        string constraintName,
        string expression
    )
        : base(constraintName)
    {
        SchemaName = schemaName;
        TableName = tableName;
        ColumnName = columnName;
        Expression = string.IsNullOrWhiteSpace(expression)
            ? throw new ArgumentException("Expression is required")
            : expression;
    }

    /// <summary>
    /// Gets or sets the schema name.
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Gets or sets the table name.
    /// </summary>
    public required string TableName { get; set; }

    /// <summary>
    /// Gets or sets the column name.
    /// </summary>
    public string? ColumnName { get; set; }

    /// <summary>
    /// Gets or sets the expression.
    /// </summary>
    public required string Expression { get; set; }

    /// <summary>
    /// Gets the constraint type.
    /// </summary>
    public override DmConstraintType ConstraintType => DmConstraintType.Check;

    /// <summary>
    /// Returns a string representation of the constraint.
    /// </summary>
    /// <returns>A string representation of the constraint.</returns>
    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(ColumnName))
        {
            return $"{ConstraintType} Constraint on {TableName} with expression: {Expression}";
        }

        return $"{ConstraintType} Constraint on {TableName}.{ColumnName} with expression: {Expression}";
    }
}
