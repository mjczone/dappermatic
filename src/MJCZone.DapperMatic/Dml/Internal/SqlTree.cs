// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

#pragma warning disable SA1313 // Positional record parameters are properties, so they are named as properties.
#pragma warning disable SA1402 // The tree's node types belong together.
#pragma warning disable SA1649

using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// A node of a query: what a C# expression or a REST query string translates to, before it is written as SQL for a
/// provider by <see cref="SqlRenderer"/>.
/// </summary>
/// <remarks>
/// Nodes hold names and values, never SQL written from user input: columns are resolved against a model or a table
/// read from the database, and values become parameters when rendered.
/// </remarks>
internal abstract record SqlNode
{
    /// <summary>
    /// Tells whether a node reads a subquery, anywhere within it.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>True if it does.</returns>
    public static bool HasSubquery(SqlNode? node) =>
        node switch
        {
            null => false,
            SqlScalar or SqlExists => true,
            SqlLogical logical => logical.Operands.Any(HasSubquery),
            SqlNot not => HasSubquery(not.Operand),
            SqlTrusted trusted => HasSubquery(trusted.Inner),
            SqlCompare compare => HasSubquery(compare.Left) || HasSubquery(compare.Right),
            SqlIsNull isNull => HasSubquery(isNull.Operand),
            SqlIn @in => HasSubquery(@in.Operand),
            SqlLike like => HasSubquery(like.Operand),
            SqlArithmetic arithmetic => HasSubquery(arithmetic.Left) || HasSubquery(arithmetic.Right),
            SqlNegate negate => HasSubquery(negate.Operand),
            SqlFunction function => function.Arguments.Any(HasSubquery),
            SqlDialectFunction function => function.Arguments.Any(HasSubquery),
            SqlCase @case => HasSubquery(@case.When) || HasSubquery(@case.Then) || HasSubquery(@case.Else),
            SqlAggregate aggregate => HasSubquery(aggregate.Argument),
            _ => false,
        };

    /// <summary>
    /// Replaces columns of a condition over a row, such as a policy's check: with the values a write gives them.
    /// </summary>
    /// <param name="node">The condition.</param>
    /// <param name="replace">What a column is replaced with, or null to keep it.</param>
    /// <returns>The condition, with the columns replaced.</returns>
    public static SqlNode ReplaceColumns(SqlNode node, Func<SqlColumn, SqlNode?> replace) =>
        node switch
        {
            SqlColumn column => replace(column) ?? column,
            SqlTrusted trusted => trusted with { Inner = ReplaceColumns(trusted.Inner, replace) },
            SqlLogical logical => logical with { Operands = [.. logical.Operands.Select(o => ReplaceColumns(o, replace))] },
            SqlNot not => not with { Operand = ReplaceColumns(not.Operand, replace) },
            SqlCompare compare => compare with { Left = ReplaceColumns(compare.Left, replace), Right = ReplaceColumns(compare.Right, replace) },
            SqlIsNull isNull => isNull with { Operand = ReplaceColumns(isNull.Operand, replace) },
            SqlIn @in => @in with { Operand = ReplaceColumns(@in.Operand, replace) },
            SqlLike like => like with { Operand = ReplaceColumns(like.Operand, replace) },
            SqlArithmetic arithmetic => arithmetic with { Left = ReplaceColumns(arithmetic.Left, replace), Right = ReplaceColumns(arithmetic.Right, replace) },
            SqlNegate negate => negate with { Operand = ReplaceColumns(negate.Operand, replace) },
            SqlFunction function => function with { Arguments = [.. function.Arguments.Select(a => ReplaceColumns(a, replace))] },
            SqlDialectFunction function => function with { Arguments = [.. function.Arguments.Select(a => ReplaceColumns(a, replace))] },
            SqlCase @case => @case with
            {
                When = ReplaceColumns(@case.When, replace),
                Then = ReplaceColumns(@case.Then, replace),
                Else = ReplaceColumns(@case.Else, replace),
            },
            _ => node,
        };
}

/// <summary>
/// A table or view a statement reads or writes.
/// </summary>
/// <param name="QualifiedName">The table, quoted and schema-qualified.</param>
/// <param name="Alias">The alias columns are qualified with when a statement reads several sources.</param>
/// <param name="Table">The table's definition: the model's, or one read from the database.</param>
/// <param name="Model">The model the table is built from, or null for a table read from the database.</param>
internal sealed record SqlSource(string QualifiedName, string Alias, DmTable Table, DmlModel? Model)
{
    /// <summary>Gets a value indicating whether the source's row may be missing, as a <c>LEFT JOIN</c>'s may.</summary>
    public bool Optional { get; init; }

    /// <summary>
    /// Gets the columns the statement's caller may not read: read as empty, never compared; null for none.
    /// </summary>
    public IReadOnlySet<string>? Hidden { get; init; }
}

/// <summary>
/// A column of a source.
/// </summary>
/// <param name="Source">The source.</param>
/// <param name="ColumnName">The column, as the model or table names it.</param>
internal sealed record SqlColumn(SqlSource Source, string ColumnName) : SqlNode;

/// <summary>
/// A value, bound as a parameter of its declared type so that the type's handler applies.
/// </summary>
/// <param name="DeclaredType">The type the value is bound as.</param>
/// <param name="Value">The value.</param>
internal sealed record SqlValue(Type DeclaredType, object? Value) : SqlNode;

/// <summary>
/// SQL the translator writes itself, never from input: <c>1 = 0</c>, <c>NULL</c>, <c>''</c>.
/// </summary>
/// <param name="Sql">The SQL.</param>
internal sealed record SqlText(string Sql) : SqlNode
{
    /// <summary>Gets a condition that is always true.</summary>
    public static SqlText True { get; } = new("1 = 1");

    /// <summary>Gets a condition that is always false.</summary>
    public static SqlText False { get; } = new("1 = 0");

    /// <summary>
    /// Gets a condition that is always true or always false.
    /// </summary>
    /// <param name="value">Which.</param>
    /// <returns>The condition.</returns>
    public static SqlText Always(bool value) => value ? True : False;
}

/// <summary>
/// <c>AND</c> or <c>OR</c> of conditions, parenthesized: <c>(a AND b AND c)</c>.
/// </summary>
/// <param name="Operator"><c>AND</c> or <c>OR</c>.</param>
/// <param name="Operands">The conditions.</param>
internal sealed record SqlLogical(string Operator, IReadOnlyList<SqlNode> Operands) : SqlNode;

/// <summary>
/// <c>NOT (condition)</c>.
/// </summary>
/// <param name="Operand">The condition.</param>
internal sealed record SqlNot(SqlNode Operand) : SqlNode;

/// <summary>
/// A condition written by the provider's own syntax, such as a full-text search: trusted SQL (read from the
/// database's own index definitions, never from a request) with its columns and values as nodes, at {0}, {1}...
/// </summary>
/// <param name="Format">The SQL, its nodes' places marked as string.Format marks them.</param>
/// <param name="Arguments">The nodes.</param>
internal sealed record SqlRaw(string Format, IReadOnlyList<SqlNode> Arguments) : SqlNode;

/// <summary>
/// A condition of a policy's, which may compare columns the caller may not read: the policy's author wrote it, not
/// the caller.
/// </summary>
/// <param name="Inner">The condition.</param>
internal sealed record SqlTrusted(SqlNode Inner) : SqlNode;

/// <summary>
/// A comparison: <c>left = right</c>, unparenthesized.
/// </summary>
/// <param name="Left">The left side.</param>
/// <param name="Operator"><c>=</c>, <c>&lt;&gt;</c>, <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c> or <c>&gt;=</c>.</param>
/// <param name="Right">The right side.</param>
internal sealed record SqlCompare(SqlNode Left, string Operator, SqlNode Right) : SqlNode;

/// <summary>
/// <c>operand IS NULL</c>, or <c>IS NOT NULL</c>.
/// </summary>
/// <param name="Operand">The operand.</param>
/// <param name="Negated">Whether it is <c>IS NOT NULL</c>.</param>
internal sealed record SqlIsNull(SqlNode Operand, bool Negated) : SqlNode;

/// <summary>
/// <c>operand IN (values)</c>, or <c>NOT IN</c>; the renderer may send a long list as one JSON parameter.
/// </summary>
/// <param name="Operand">The operand.</param>
/// <param name="Values">The values, none of them null.</param>
/// <param name="Negated">Whether it is <c>NOT IN</c>.</param>
internal sealed record SqlIn(SqlNode Operand, IReadOnlyList<SqlValue> Values, bool Negated) : SqlNode;

/// <summary>
/// <c>operand LIKE pattern ESCAPE 'c'</c>, or <c>NOT LIKE</c>.
/// </summary>
/// <param name="Operand">The operand.</param>
/// <param name="Pattern">The pattern, its wildcard characters already escaped.</param>
/// <param name="Escape">The escape character, one the value does not contain.</param>
/// <param name="Negated">Whether it is <c>NOT LIKE</c>.</param>
internal sealed record SqlLike(SqlNode Operand, SqlValue Pattern, char Escape, bool Negated) : SqlNode;

/// <summary>
/// Arithmetic: <c>(left op right)</c>.
/// </summary>
/// <param name="Left">The left side.</param>
/// <param name="Operator"><c>+</c>, <c>-</c>, <c>*</c>, <c>/</c> or <c>%</c>.</param>
/// <param name="Right">The right side.</param>
internal sealed record SqlArithmetic(SqlNode Left, string Operator, SqlNode Right) : SqlNode;

/// <summary>
/// <c>(-operand)</c>.
/// </summary>
/// <param name="Operand">The operand.</param>
internal sealed record SqlNegate(SqlNode Operand) : SqlNode;

/// <summary>
/// A function every provider spells the same: <c>UPPER(x)</c>, <c>COALESCE(a, b)</c>, <c>REPLACE(x, a, b)</c>.
/// </summary>
/// <param name="Name">The function.</param>
/// <param name="Arguments">Its arguments.</param>
internal sealed record SqlFunction(string Name, IReadOnlyList<SqlNode> Arguments) : SqlNode;

/// <summary>
/// A function each provider spells its own way, written by <see cref="DmlDialect"/>.
/// </summary>
/// <param name="Function">The function.</param>
/// <param name="Arguments">Its arguments.</param>
/// <param name="Option">A function's option: the date part, or which ends a trim trims.</param>
internal sealed record SqlDialectFunction(DialectFunction Function, IReadOnlyList<SqlNode> Arguments, string? Option = null)
    : SqlNode;

/// <summary>
/// <c>CASE WHEN condition THEN a ELSE b END</c>.
/// </summary>
/// <param name="When">The condition.</param>
/// <param name="Then">The value when it holds.</param>
/// <param name="Else">The value otherwise.</param>
internal sealed record SqlCase(SqlNode When, SqlNode Then, SqlNode Else) : SqlNode;

/// <summary>
/// An aggregate: <c>COUNT(*)</c>, <c>SUM(x)</c>, <c>MIN(x)</c>, <c>MAX(x)</c>, or an average with a fraction.
/// </summary>
/// <param name="Function"><c>COUNT</c>, <c>SUM</c>, <c>MIN</c>, <c>MAX</c> or <c>AVG</c>.</param>
/// <param name="Argument">The argument, or null for <c>COUNT(*)</c>.</param>
/// <param name="WithFraction">For <c>AVG</c>, whether integers average with a fraction.</param>
/// <param name="Distinct">Whether to aggregate each distinct value once, as COUNT(DISTINCT x).</param>
internal sealed record SqlAggregate(string Function, SqlNode? Argument, bool WithFraction = false, bool Distinct = false) : SqlNode;

/// <summary>
/// <c>EXISTS (select)</c>.
/// </summary>
/// <param name="Select">The subquery.</param>
internal sealed record SqlExists(SqlSelect Select) : SqlNode;

/// <summary>
/// A subquery read as one value: <c>(SELECT t1."name" FROM ...)</c>, NULL when it returns no row.
/// </summary>
/// <param name="Select">The subquery, of one item.</param>
internal sealed record SqlScalar(SqlSelect Select) : SqlNode;

/// <summary>
/// <c>ROW_NUMBER() OVER (PARTITION BY ... ORDER BY ...)</c>: the rows' order within each partition, from 1.
/// </summary>
/// <param name="PartitionBy">The partition keys.</param>
/// <param name="OrderBy">The order within a partition; at least one key, which SQL Server requires.</param>
internal sealed record SqlRowNumber(IReadOnlyList<SqlNode> PartitionBy, IReadOnlyList<SqlOrder> OrderBy) : SqlNode;

/// <summary>
/// A source joined into a <c>SELECT</c>.
/// </summary>
/// <param name="Source">The source joined.</param>
/// <param name="Kind"><c>LEFT JOIN</c>, <c>INNER JOIN</c> or <c>CROSS JOIN</c>.</param>
/// <param name="On">The join condition, or null for a cross join.</param>
internal sealed record SqlJoin(SqlSource Source, string Kind, SqlNode? On);

/// <summary>
/// The functions <see cref="SqlDialectFunction"/> writes per provider.
/// </summary>
internal enum DialectFunction
{
    /// <summary>The length of a string, trailing spaces included.</summary>
    Length,

    /// <summary>Trims spaces: option <c>start</c>, <c>end</c> or <c>both</c>.</summary>
    Trim,

    /// <summary>Part of a string: text, first character from 1, and optionally a length.</summary>
    Substring,

    /// <summary>A date part, as an integer: option <c>Year</c> ... <c>Second</c>.</summary>
    DatePart,

    /// <summary>Strings joined, none of them NULL.</summary>
    Concat,

    /// <summary>A test for exactly the empty string.</summary>
    IsEmptyString,

    /// <summary>Integer division, dropping the remainder.</summary>
    IntegerDivide,

    /// <summary>Division with a fraction of two integers.</summary>
    FractionDivide,

    /// <summary>The remainder of numbers with fractions, after a division truncated toward zero.</summary>
    FractionRemainder,
}

/// <summary>
/// An item of a select list: a column or a value, and the name it is read as.
/// </summary>
/// <param name="Node">The column or value.</param>
/// <param name="Alias">The name it is read as, or null for none.</param>
internal sealed record SqlSelectItem(SqlNode Node, string? Alias);

/// <summary>
/// An ordering key.
/// </summary>
/// <param name="Key">The key.</param>
/// <param name="Descending">Whether the order is descending.</param>
/// <param name="Nullable">Whether the key can be NULL, so that NULLs can be put where LINQ puts them.</param>
internal sealed record SqlOrder(SqlNode Key, bool Descending, bool Nullable);

/// <summary>
/// A <c>SELECT</c>, rendered as one statement or as a subquery.
/// </summary>
internal sealed record SqlSelect
{
    /// <summary>Gets the source read from.</summary>
    public required SqlSource From { get; init; }

    /// <summary>Gets a subquery read from in place of the source's table, as <c>(subquery) q</c>.</summary>
    public SqlSelect? FromSubquery { get; init; }

    /// <summary>Gets the sources joined to the source, in order.</summary>
    public IReadOnlyList<SqlJoin> Joins { get; init; } = [];

    /// <summary>Gets the select list; empty for <c>SELECT 1</c>.</summary>
    public IReadOnlyList<SqlSelectItem> Items { get; init; } = [];

    /// <summary>Gets a value indicating whether the rows are distinct.</summary>
    public bool Distinct { get; init; }

    /// <summary>Gets the conditions, ANDed; empty for none.</summary>
    public IReadOnlyList<SqlNode> Where { get; init; } = [];

    /// <summary>Gets the grouping keys; empty for none.</summary>
    public IReadOnlyList<SqlNode> GroupBy { get; init; } = [];

    /// <summary>Gets the conditions on the groups, ANDed; empty for none.</summary>
    public IReadOnlyList<SqlNode> Having { get; init; } = [];

    /// <summary>Gets the ordering keys.</summary>
    public IReadOnlyList<SqlOrder> OrderBy { get; init; } = [];

    /// <summary>Gets the ordering in its own SQL, for an order the translator writes (key order, an ordinal).</summary>
    public string? OrderBySql { get; init; }

    /// <summary>Gets the rows to skip, or null.</summary>
    public int? Skip { get; init; }

    /// <summary>Gets the most rows to return, or null.</summary>
    public int? Take { get; init; }
}
