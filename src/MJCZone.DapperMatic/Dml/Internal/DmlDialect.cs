// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Globalization;
using MJCZone.DapperMatic.Providers;
using MJCZone.DapperMatic.Providers.Base;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// The SQL that differs between providers.
/// </summary>
internal sealed class DmlDialect
{
    private static readonly Dictionary<string, string> SqliteFormats = new()
    {
        ["Year"] = "%Y",
        ["Month"] = "%m",
        ["Day"] = "%d",
        ["Hour"] = "%H",
        ["Minute"] = "%M",
        ["Second"] = "%S",
    };

    private readonly IDbConnection _connection;
    private readonly DatabaseMethodsBase _methods;

    private DmlDialect(IDbConnection connection)
    {
        _connection = connection;
        ProviderType = connection.GetDbProviderType();
        _methods =
            DatabaseMethodsProvider.GetMethods(connection) as DatabaseMethodsBase
            ?? throw new NotSupportedException(
                $"{connection.GetType().Name} is not a connection the DML methods know how to write SQL for."
            );
    }

    /// <summary>
    /// Gets the provider.
    /// </summary>
    public DbProviderType ProviderType { get; }

    /// <summary>
    /// Gets the most parameters one statement may have, with a margin: SQL Server allows 2,100, SQLite 32,766 (from
    /// 3.32; both drivers bundle a later one), and PostgreSQL and MySQL 65,535.
    /// </summary>
    public int MaxParameters =>
        ProviderType switch
        {
            DbProviderType.SqlServer => 2_000,
            DbProviderType.Sqlite => 32_000,
            _ => 60_000,
        };

    /// <summary>
    /// Gets the most rows one <c>INSERT ... VALUES</c> may have: SQL Server allows 1,000 row constructors.
    /// </summary>
    public int MaxRowsPerInsert => 1_000;

    /// <summary>
    /// Gets the dialect for a connection.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <returns>The dialect.</returns>
    public static DmlDialect For(IDbConnection connection) => new(connection);

    /// <summary>
    /// Names a table, schema-qualified where the provider has schemas.
    /// </summary>
    /// <param name="schemaName">The schema, or null for the default.</param>
    /// <param name="tableName">The table.</param>
    /// <returns>The quoted, qualified name.</returns>
    public string Table(string? schemaName, string tableName) =>
        _methods.GetSchemaQualifiedIdentifierName(schemaName, tableName);

    /// <summary>
    /// Names a column the way DDL created it.
    /// </summary>
    /// <param name="columnName">The column name, as the model gives it.</param>
    /// <returns>The quoted name.</returns>
    public string Column(string columnName) => _methods.GetNormalizedQuotedIdentifier(columnName);

    /// <summary>
    /// Names a column the way DDL created it, unquoted, for APIs that take a column by name.
    /// </summary>
    /// <param name="columnName">The column name, as the model gives it.</param>
    /// <returns>The normalized name.</returns>
    public string ColumnName(string columnName) => _methods.GetNormalizedName(columnName);

    /// <summary>
    /// Quotes an alias, keeping its case, for a column read into the property of that name.
    /// </summary>
    /// <param name="alias">The alias.</param>
    /// <returns>The quoted alias.</returns>
    public string Alias(string alias) => _connection.GetQuotedIdentifier(alias);

    /// <summary>
    /// Turns an <c>INSERT</c> into one that also returns the key the database generated.
    /// </summary>
    /// <param name="insert">The <c>INSERT</c> statement.</param>
    /// <param name="keyColumn">The key column, quoted.</param>
    /// <returns>The statement and query, as one command.</returns>
    public string ReturningGeneratedKey(string insert, string keyColumn) =>
        ProviderType switch
        {
            // SCOPE_IDENTITY, unlike OUTPUT without INTO, works on a table with triggers.
            DbProviderType.SqlServer => $"{insert}; SELECT CAST(SCOPE_IDENTITY() AS bigint)",
            DbProviderType.PostgreSql => $"{insert} RETURNING {keyColumn}",
            DbProviderType.MySql => $"{insert}; SELECT LAST_INSERT_ID()",
            DbProviderType.Sqlite => $"{insert}; SELECT last_insert_rowid()",
            _ => throw new NotSupportedException($"Returning a generated key is not supported on {ProviderType}."),
        };

    /// <summary>
    /// Refers to a parameter in a translated expression. Both SQLite drivers send a decimal as TEXT, which a column's
    /// NUMERIC affinity turns back into a number, but a computed value has no affinity: <c>total * 2 &gt; '50'</c>
    /// compares a number with text, and SQLite orders every number before every text.
    /// </summary>
    /// <param name="name">The parameter's name, without a prefix.</param>
    /// <param name="declaredType">The type it is bound as.</param>
    /// <returns>The SQL.</returns>
    public string ParameterReference(string name, Type declaredType) =>
        ProviderType == DbProviderType.Sqlite && (Nullable.GetUnderlyingType(declaredType) ?? declaredType) == typeof(decimal)
            ? $"CAST(@{name} AS NUMERIC)"
            : "@" + name;

    /// <summary>
    /// Tests a string column for the empty string exactly. SQL Server, and MySQL with a PAD SPACE collation, ignore
    /// trailing spaces in <c>=</c>, so <c>'  ' = ''</c> is true there.
    /// </summary>
    /// <param name="column">The column, quoted.</param>
    /// <returns>The condition.</returns>
    public string IsEmptyString(string column) =>
        ProviderType switch
        {
            DbProviderType.SqlServer => $"DATALENGTH({column}) = 0",
            DbProviderType.MySql => $"CHAR_LENGTH({column}) = 0",
            _ => $"{column} = ''",
        };

    /// <summary>
    /// Counts the characters of a string, trailing spaces included, as <see cref="string.Length"/> does: SQL Server's
    /// LEN leaves them out.
    /// </summary>
    /// <param name="text">The string.</param>
    /// <returns>The SQL.</returns>
    public string Length(string text) =>
        ProviderType switch
        {
            DbProviderType.SqlServer => $"(LEN({text} + N'.') - 1)",
            DbProviderType.Sqlite => $"LENGTH({text})",
            _ => $"CHAR_LENGTH({text})",
        };

    /// <summary>
    /// Joins strings.
    /// </summary>
    /// <param name="parts">The strings, none of them NULL.</param>
    /// <returns>The SQL.</returns>
    public string Concat(IReadOnlyList<string> parts) =>
        parts.Count == 1 ? parts[0]
        : ProviderType switch
        {
            DbProviderType.SqlServer => $"({string.Join(" + ", parts)})",
            DbProviderType.MySql => $"CONCAT({string.Join(", ", parts)})",
            _ => $"({string.Join(" || ", parts)})",
        };

    /// <summary>
    /// Trims the spaces from either end of a string, or both.
    /// </summary>
    /// <param name="text">The string.</param>
    /// <param name="start">Whether to trim the start.</param>
    /// <param name="end">Whether to trim the end.</param>
    /// <returns>The SQL.</returns>
    public string Trim(string text, bool start, bool end) =>
        (start, end) switch
        {
            (true, false) => $"LTRIM({text})",
            (false, true) => $"RTRIM({text})",

            // TRIM is SQL Server 2017 and later.
            _ when ProviderType == DbProviderType.SqlServer => $"LTRIM(RTRIM({text}))",
            _ => $"TRIM({text})",
        };

    /// <summary>
    /// Takes part of a string.
    /// </summary>
    /// <param name="text">The string.</param>
    /// <param name="start">The first character, counting from 1.</param>
    /// <param name="length">The number of characters, or null for the rest.</param>
    /// <returns>The SQL.</returns>
    public string Substring(string text, string start, string? length) =>
        ProviderType switch
        {
            // SQL Server's SUBSTRING needs a length.
            DbProviderType.SqlServer => $"SUBSTRING({text}, {start}, {length ?? "2147483647"})",
            DbProviderType.Sqlite => length is null ? $"SUBSTR({text}, {start})" : $"SUBSTR({text}, {start}, {length})",
            _ => length is null ? $"SUBSTRING({text}, {start})" : $"SUBSTRING({text}, {start}, {length})",
        };

    /// <summary>
    /// Takes a part of a date or time as an integer.
    /// </summary>
    /// <param name="part">Year, Month, Day, Hour, Minute or Second.</param>
    /// <param name="value">The date or time.</param>
    /// <returns>The SQL.</returns>
    public string DatePart(string part, string value) =>
        ProviderType switch
        {
            DbProviderType.SqlServer => $"DATEPART({part.ToLowerInvariant()}, {value})",
            DbProviderType.MySql => $"{part.ToUpperInvariant()}({value})",

            // EXTRACT(SECOND ...) has the fraction too, which DateTime.Second does not.
            DbProviderType.PostgreSql => part == "Second"
                ? $"CAST(FLOOR(EXTRACT(SECOND FROM {value})) AS integer)"
                : $"CAST(EXTRACT({part.ToUpperInvariant()} FROM {value}) AS integer)",
            _ => $"CAST(strftime('{SqliteFormats[part]}', {value}) AS INTEGER)",
        };

    /// <summary>
    /// Divides integers, dropping the remainder as C# does: MySQL's / keeps the fraction.
    /// </summary>
    /// <param name="left">The dividend.</param>
    /// <param name="right">The divisor.</param>
    /// <returns>The SQL.</returns>
    public string IntegerDivide(string left, string right) =>
        ProviderType == DbProviderType.MySql ? $"({left} DIV {right})" : $"({left} / {right})";

    /// <summary>
    /// Writes an ordering key, with NULLs first ascending and last descending, as LINQ orders them: PostgreSQL alone
    /// orders the other way by default.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="descending">Whether the order is descending.</param>
    /// <param name="nullable">Whether the key can be NULL.</param>
    /// <returns>The SQL.</returns>
    public string OrderKey(string key, bool descending, bool nullable) =>
        (ProviderType == DbProviderType.PostgreSql && nullable, descending) switch
        {
            (true, false) => $"{key} ASC NULLS FIRST",
            (true, true) => $"{key} DESC NULLS LAST",
            (false, true) => $"{key} DESC",
            _ => key,
        };

    /// <summary>
    /// Writes an average with a fraction: SQL Server averages integers as an integer, and MySQL as four places.
    /// </summary>
    /// <param name="value">The values to average.</param>
    /// <returns>The SQL.</returns>
    public string AverageWithFraction(string value) =>
        ProviderType switch
        {
            DbProviderType.SqlServer => $"AVG(CAST({value} AS float))",

            // MySQL averages integers as a DECIMAL of four places (div_precision_increment): 0.3333 for 1/3.
            DbProviderType.MySql => $"AVG({value} * 1.0E0)",
            _ => $"AVG({value})",
        };

    /// <summary>
    /// Writes the remainder of numbers with fractions, as C#'s <c>%</c> computes it: SQLite's <c>%</c> works on
    /// integers.
    /// </summary>
    /// <param name="left">The dividend.</param>
    /// <param name="right">The divisor.</param>
    /// <returns>The SQL.</returns>
    public string FractionRemainder(string left, string right) =>
        ProviderType == DbProviderType.Sqlite
            ? $"({left} - {right} * CAST({left} * 1.0 / {right} AS INTEGER))"
            : $"({left} % {right})";

    /// <summary>
    /// Orders and pages a <c>SELECT</c>.
    /// </summary>
    /// <param name="select">The statement.</param>
    /// <param name="orderBy">The <c>ORDER BY</c> list, or null.</param>
    /// <param name="skip">The rows to skip, or null.</param>
    /// <param name="take">The most rows to return, or null.</param>
    /// <returns>The statement, ordered and paged.</returns>
    public string Page(string select, string? orderBy, int? skip, int? take)
    {
        var sql = new System.Text.StringBuilder(select);
        if (ProviderType == DbProviderType.SqlServer)
        {
            if (skip is null && take is null)
            {
                return orderBy is null ? select : $"{select} ORDER BY {orderBy}";
            }

            // OFFSET needs an ORDER BY; (SELECT NULL) is the documented way to page in no particular order.
            sql.Append(CultureInfo.InvariantCulture, $" ORDER BY {orderBy ?? "(SELECT NULL)"} OFFSET {skip ?? 0} ROWS");
            if (take is not null)
            {
                sql.Append(CultureInfo.InvariantCulture, $" FETCH NEXT {take} ROWS ONLY");
            }

            return sql.ToString();
        }

        if (orderBy is not null)
        {
            sql.Append(" ORDER BY ").Append(orderBy);
        }

        if (take is not null)
        {
            sql.Append(CultureInfo.InvariantCulture, $" LIMIT {take}");
        }
        else if (skip is not null && ProviderType != DbProviderType.PostgreSql)
        {
            // MySQL and SQLite take OFFSET only after a LIMIT: these are their documented "no limit" values.
            sql.Append(ProviderType == DbProviderType.Sqlite ? " LIMIT -1" : " LIMIT 18446744073709551615");
        }

        if (skip is not null)
        {
            sql.Append(CultureInfo.InvariantCulture, $" OFFSET {skip}");
        }

        return sql.ToString();
    }

    /// <summary>
    /// Writes an <c>INSERT</c> of a row with no values of its own, every column taking its default.
    /// </summary>
    /// <param name="table">The table, quoted.</param>
    /// <returns>The statement.</returns>
    public string InsertDefaults(string table) =>
        ProviderType == DbProviderType.MySql ? $"INSERT INTO {table} () VALUES ()" : $"INSERT INTO {table} DEFAULT VALUES";
}
