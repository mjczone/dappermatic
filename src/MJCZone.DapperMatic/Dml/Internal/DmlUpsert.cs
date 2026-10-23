// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// Writes upserts and inserts-if-absent of one row for each provider.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>PostgreSQL and SQLite: <c>INSERT ... AS t ... ON CONFLICT (target) DO UPDATE SET ... WHERE ... RETURNING</c>,
/// the incoming row as <c>excluded</c>.</item>
/// <item>MySQL and MariaDB: <c>ON DUPLICATE KEY UPDATE</c>, the incoming row as <c>VALUES(col)</c>; a condition becomes
/// <c>col = IF(condition, new, col)</c> per column, the condition's own columns assigned last because MySQL assigns
/// left to right; <c>key = LAST_INSERT_ID(key)</c> makes the existing row's key the one returned.</item>
/// <item>SQL Server: <c>IF EXISTS (... WITH (UPDLOCK, SERIALIZABLE)) UPDATE ... ELSE INSERT</c> in one transaction, the
/// incoming row as its parameters, so that a conditional update never falls through to an insert.</item>
/// </list>
/// </remarks>
internal static class DmlUpsert
{
    /// <summary>
    /// Finds the columns a conflict is on, and checks they are the primary key or a unique constraint or index.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="onConflict">The columns, or null.</param>
    /// <param name="constraint">The name of a unique constraint or index, or null.</param>
    /// <returns>The columns, and the name of the constraint if one was named.</returns>
    /// <exception cref="ArgumentException">The columns are not unique together, or the constraint is unknown.</exception>
    public static (IReadOnlyList<DmlColumn> Columns, string? Constraint) Target(
        DmlModel model,
        LambdaExpression? onConflict,
        string? constraint
    )
    {
        var uniqueSets = model.UniqueColumnSets();
        if (constraint is not null)
        {
            var named = uniqueSets.FirstOrDefault(u => string.Equals(u.Name, constraint, StringComparison.OrdinalIgnoreCase));
            return named.Columns is null
                ? throw new ArgumentException(
                    $"{model.Type.Name} has no primary key, unique constraint or unique index named {constraint}.",
                    nameof(constraint)
                )
                : (named.Columns, named.Name);
        }

        if (onConflict is null)
        {
            model.RequireKey("An upsert on the primary key");
            return (model.Keys, null);
        }

        var columns = ColumnsOf(model, onConflict);
        var unique = uniqueSets.Any(u => u.Columns.Count == columns.Count && u.Columns.All(columns.Contains));
        return unique
            ? (columns, null)
            : throw new ArgumentException(
                $"{model.Type.Name} has no primary key, unique constraint or unique index on exactly "
                    + $"({string.Join(", ", columns.Select(c => c.Property.Name))}): PostgreSQL and SQLite need one to "
                    + "find a conflict, and MySQL would update on any unique key.",
                nameof(onConflict)
            );
    }

    /// <summary>
    /// Makes what an upsert or insert-if-absent of a row writes: every column but a generated key, with the row's
    /// values, bound by each property's type.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="dialect">The dialect.</param>
    /// <param name="table">The table, quoted and qualified.</param>
    /// <param name="row">The row.</param>
    /// <param name="target">The conflict columns.</param>
    /// <param name="constraint">The named constraint the conflict is on, or null.</param>
    /// <returns>The statement, with no update or condition.</returns>
    public static DmlUpsertStatement Statement(
        DmlModel model,
        DmlDialect dialect,
        string table,
        object row,
        IReadOnlyList<DmlColumn> target,
        string? constraint
    )
    {
        var written = model.Behaviours.DatabaseWritesToken(dialect) ? model.Behaviours.Token : null;
        var columns = model.Columns.Where(c => c != model.Generated && c != written).ToList();
        return new DmlUpsertStatement
        {
            Model = model,
            Dialect = dialect,
            Table = table,
            Columns = columns,
            Values = [.. columns.Select(c => new SqlValue(c.Property.PropertyType, c.Property.GetValue(row)))],
            Target = target,
            Constraint = constraint,
            ReturnKey = model.Generated,
        };
    }

    /// <summary>
    /// Writes an upsert of one row.
    /// </summary>
    /// <param name="statement">What to write.</param>
    /// <param name="parameters">The parameters, which the values are added to.</param>
    /// <returns>The SQL; when <see cref="DmlUpsertStatement.ReturnKey"/> is set, a query returning the row's key.</returns>
    public static string Upsert(DmlUpsertStatement statement, DmlParameters parameters)
    {
        var (model, dialect, table) = (statement.Model, statement.Dialect, statement.Table);
        var current = new SqlSource(table, "t", model.Table, model);
        var incoming = new SqlSource("excluded", "excluded", model.Table, model);
        var valueOf = statement.Columns.Zip(statement.Values).ToDictionary(p => p.First.Column.ColumnName, p => p.Second);

        List<(SqlColumn Column, SqlNode Value)> assignments = statement.Update is not null
            ? DmlTranslator.Assignments(statement.Update, current, [current, incoming], dialect)
            : statement.Columns
                .Where(c => !statement.Target.Contains(c) && !model.Keys.Contains(c))
                .Select(c => (new SqlColumn(current, c.Column.ColumnName), (SqlNode)new SqlColumn(incoming, c.Column.ColumnName)))
                .ToList();
        var condition = statement.Finish(
            assignments,
            statement.UpdateWhere is null ? null : DmlTranslator.Condition(statement.UpdateWhere, [current, incoming], dialect),
            current,
            incoming
        );

        SqlRenderer renderer = null!;
        renderer = new SqlRenderer(dialect, parameters)
        {
            ColumnWriter = c =>
                (ReferenceEquals(c.Source, incoming), dialect.ProviderType) switch
                {
                    (true, DbProviderType.SqlServer) => renderer.Render(valueOf[c.ColumnName]),
                    (true, DbProviderType.MySql) => $"VALUES({dialect.Column(c.ColumnName)})",
                    (true, _) => $"excluded.{dialect.Column(c.ColumnName)}",
                    (false, DbProviderType.PostgreSql or DbProviderType.Sqlite) => $"t.{dialect.Column(c.ColumnName)}",
                    _ => null,
                },
        }.For(statement.Values, assignments.ConvertAll(a => a.Value), condition);

        var columns = string.Join(", ", statement.Columns.Select(c => dialect.Column(c.Column.ColumnName)));
        var values = string.Join(", ", statement.Values.Select(renderer.Render));
        var key = statement.ReturnKey is null ? null : dialect.Column(statement.ReturnKey.Column.ColumnName);
        string Set((SqlColumn Column, SqlNode Value) a) => $"{dialect.Column(a.Column.ColumnName)} = {renderer.Render(a.Value)}";

        switch (dialect.ProviderType)
        {
            case DbProviderType.SqlServer:
                {
                    var match = Match(statement, renderer);
                    var update = assignments.Count == 0
                        ? string.Empty
                        : $"UPDATE {table} SET {string.Join(", ", assignments.Select(Set))} WHERE {match}"
                            + (condition is null ? string.Empty : $" AND {renderer.Render(condition)}")
                            + "; ";
                    // The key of a row another tenant has is not this scope's to return.
                    var guard = statement.Guard?.Invoke(current);
                    var selectKey = key is null
                        ? string.Empty
                        : $"SELECT {key} FROM {table} WHERE {match}" + (guard is null ? string.Empty : $" AND {renderer.Render(guard)}") + "; ";
                    var insertKey = key is null ? string.Empty : "SELECT CAST(SCOPE_IDENTITY() AS bigint); ";
                    return $"IF EXISTS (SELECT 1 FROM {table} WITH (UPDLOCK, SERIALIZABLE) WHERE {match}) "
                        + $"BEGIN {update}{selectKey}END "
                        + $"ELSE BEGIN INSERT INTO {table} ({columns}) VALUES ({values}); {insertKey}END";
                }

            case DbProviderType.MySql:
                {
                    // MySQL assigns left to right, each assignment seeing the ones before it: the condition's own
                    // columns go last, so that it is tested on the row as it was.
                    var conditionColumns = condition is null ? [] : CurrentColumns(condition, current);
                    var ordered = assignments.OrderBy(a => conditionColumns.Contains(a.Column.ColumnName)).ToList();
                    var conditionSql = condition is null ? null : renderer.Render(condition);
                    var sets = ordered.Select(a =>
                        conditionSql is null
                            ? Set(a)
                            : $"{dialect.Column(a.Column.ColumnName)} = IF({conditionSql}, {renderer.Render(a.Value)}, {dialect.Column(a.Column.ColumnName)})"
                    );

                    // key = LAST_INSERT_ID(key) makes LAST_INSERT_ID() the existing row's key, and assigning a column
                    // its own value keeps the statement valid with nothing else to set.
                    List<string> allSets = key is not null ? [$"{key} = LAST_INSERT_ID({key})", .. sets] : [.. sets];
                    if (allSets.Count == 0)
                    {
                        var first = dialect.Column(statement.Target[0].Column.ColumnName);
                        allSets.Add($"{first} = {first}");
                    }

                    return $"INSERT INTO {table} ({columns}) VALUES ({values}) ON DUPLICATE KEY UPDATE {string.Join(", ", allSets)}"
                        + (key is null ? string.Empty : "; SELECT LAST_INSERT_ID()");
                }

            default:
                {
                    // An update that sets nothing would be DO NOTHING, which returns no key: set a conflict column to
                    // itself instead.
                    var sets = assignments.Count == 0
                        ? $"{dialect.Column(statement.Target[0].Column.ColumnName)} = excluded.{dialect.Column(statement.Target[0].Column.ColumnName)}"
                        : string.Join(", ", assignments.Select(Set));
                    return $"INSERT INTO {table} AS t ({columns}) VALUES ({values}) ON CONFLICT {TargetClause(statement, dialect)} "
                        + $"DO UPDATE SET {sets}"
                        + (condition is null ? string.Empty : $" WHERE {renderer.Render(condition)}")
                        + (key is null ? string.Empty : $" RETURNING {key}");
                }
        }
    }

    /// <summary>
    /// Writes the merge of a staging table's rows into the statement's table: each row inserted, or the row it
    /// conflicts with updated as the statement says.
    /// </summary>
    /// <param name="statement">What to write; its values are not used.</param>
    /// <param name="stage">The staging table, quoted, with the statement's columns.</param>
    /// <param name="parameters">The parameters, which any values in the update or condition are added to.</param>
    /// <returns>The SQL.</returns>
    public static string Merge(DmlUpsertStatement statement, string stage, DmlParameters parameters)
    {
        var (model, dialect, table) = (statement.Model, statement.Dialect, statement.Table);
        var current = new SqlSource(table, "t", model.Table, model);
        var incoming = new SqlSource(stage, "s", model.Table, model);
        List<(SqlColumn Column, SqlNode Value)> assignments = statement.Update is not null
            ? DmlTranslator.Assignments(statement.Update, current, [current, incoming], dialect)
            : statement.Columns
                .Where(c => !statement.Target.Contains(c) && !model.Keys.Contains(c))
                .Select(c => (new SqlColumn(current, c.Column.ColumnName), (SqlNode)new SqlColumn(incoming, c.Column.ColumnName)))
                .ToList();
        var condition = statement.Finish(
            assignments,
            statement.UpdateWhere is null ? null : DmlTranslator.Condition(statement.UpdateWhere, [current, incoming], dialect),
            current,
            incoming
        );

        var provider = dialect.ProviderType;
        var renderer = new SqlRenderer(dialect, parameters)
        {
            // MySQL qualifies the table's own columns by its name: the stage's have the same names.
            ColumnWriter = c =>
                (ReferenceEquals(c.Source, incoming), provider) switch
                {
                    (true, DbProviderType.SqlServer) => $"s.{dialect.Column(c.ColumnName)}",
                    (true, DbProviderType.MySql) => $"VALUES({dialect.Column(c.ColumnName)})",
                    (true, _) => $"excluded.{dialect.Column(c.ColumnName)}",
                    (false, DbProviderType.MySql) => $"{table}.{dialect.Column(c.ColumnName)}",
                    _ => $"t.{dialect.Column(c.ColumnName)}",
                },
        }.For(assignments.ConvertAll(a => a.Value), condition);

        var columns = string.Join(", ", statement.Columns.Select(c => dialect.Column(c.Column.ColumnName)));
        string Set((SqlColumn Column, SqlNode Value) a) => $"{dialect.Column(a.Column.ColumnName)} = {renderer.Render(a.Value)}";
        var first = dialect.Column(statement.Target[0].Column.ColumnName);

        switch (provider)
        {
            case DbProviderType.SqlServer:
                {
                    // Update first, so that the rows just inserted are not updated again (a counter would double).
                    var on = string.Join(" AND ", statement.Target.Select(c => $"t.{dialect.Column(c.Column.ColumnName)} = s.{dialect.Column(c.Column.ColumnName)}"));
                    var update = assignments.Count == 0
                        ? string.Empty
                        : $"UPDATE t SET {string.Join(", ", assignments.Select(Set))} FROM {table} AS t WITH (UPDLOCK, SERIALIZABLE) "
                            + $"INNER JOIN {stage} AS s ON {on}"
                            + (condition is null ? string.Empty : $" WHERE {renderer.Render(condition)}")
                            + "; ";
                    return update
                        + $"INSERT INTO {table} ({columns}) SELECT {string.Join(", ", statement.Columns.Select(c => $"s.{dialect.Column(c.Column.ColumnName)}"))} "
                        + $"FROM {stage} AS s WHERE NOT EXISTS (SELECT 1 FROM {table} AS t WITH (UPDLOCK, SERIALIZABLE) WHERE {on})";
                }

            case DbProviderType.MySql:
                {
                    var conditionColumns = condition is null ? [] : CurrentColumns(condition, current);
                    var conditionSql = condition is null ? null : renderer.Render(condition);
                    var sets = assignments
                        .OrderBy(a => conditionColumns.Contains(a.Column.ColumnName))
                        .Select(a =>
                            conditionSql is null
                                ? Set(a)
                                : $"{dialect.Column(a.Column.ColumnName)} = IF({conditionSql}, {renderer.Render(a.Value)}, {table}.{dialect.Column(a.Column.ColumnName)})"
                        )
                        .ToList();
                    return $"INSERT INTO {table} ({columns}) SELECT {columns} FROM {stage} ON DUPLICATE KEY UPDATE "
                        + (sets.Count == 0 ? $"{first} = {table}.{first}" : string.Join(", ", sets));
                }

            default:
                {
                    // SQLite needs WHERE true between a SELECT and ON CONFLICT, which it would otherwise read as a join's.
                    var sets = assignments.Count == 0 ? $"{first} = excluded.{first}" : string.Join(", ", assignments.Select(Set));
                    return $"INSERT INTO {table} AS t ({columns}) SELECT {columns} FROM {stage}"
                        + (provider == DbProviderType.Sqlite ? " WHERE true" : string.Empty)
                        + $" ON CONFLICT {TargetClause(statement, dialect)} DO UPDATE SET {sets}"
                        + (condition is null ? string.Empty : $" WHERE {renderer.Render(condition)}");
                }
        }
    }

    /// <summary>
    /// Writes an insert of one row unless it conflicts.
    /// </summary>
    /// <param name="statement">What to write; its update and condition are not used.</param>
    /// <param name="parameters">The parameters, which the values are added to.</param>
    /// <returns>
    /// The SQL: a query returning a value only if the row was inserted (the key when <see cref="DmlUpsertStatement.ReturnKey"/> is
    /// set) on PostgreSQL, SQLite and SQL Server; on MySQL, a query returning the rows inserted and the last insert id.
    /// </returns>
    public static string InsertIfAbsent(DmlUpsertStatement statement, DmlParameters parameters)
    {
        var (dialect, table) = (statement.Dialect, statement.Table);
        var renderer = new SqlRenderer(dialect, parameters).For(statement.Values);
        var columns = string.Join(", ", statement.Columns.Select(c => dialect.Column(c.Column.ColumnName)));
        var values = string.Join(", ", statement.Values.Select(renderer.Render));
        var key = statement.ReturnKey is null ? null : dialect.Column(statement.ReturnKey.Column.ColumnName);

        return dialect.ProviderType switch
        {
            DbProviderType.SqlServer =>
                $"IF NOT EXISTS (SELECT 1 FROM {table} WITH (UPDLOCK, SERIALIZABLE) WHERE {Match(statement, renderer)}) "
                    + $"BEGIN INSERT INTO {table} ({columns}) VALUES ({values}); "
                    + $"SELECT {(key is null ? "1" : "CAST(SCOPE_IDENTITY() AS bigint)")}; END ELSE SELECT NULL",

            // Not INSERT IGNORE, which ignores every other error too. MySQL refuses a subquery of the table inserted
            // into (error 1093) but allows it in the FROM clause, hence the join. A race with another insert of the
            // same key ends in a duplicate-key error, which the caller reads as "present".
            DbProviderType.MySql =>
                $"INSERT INTO {table} ({columns}) SELECT {values} FROM (SELECT 1 AS one) AS v "
                    + $"LEFT JOIN {table} AS e ON {Match(statement, renderer, "e.")} "
                    + $"WHERE e.{dialect.Column(statement.Target[0].Column.ColumnName)} IS NULL; "
                    + "SELECT ROW_COUNT(), LAST_INSERT_ID()",

            _ => $"INSERT INTO {table} ({columns}) VALUES ({values}) ON CONFLICT {TargetClause(statement, dialect)} DO NOTHING"
                + $" RETURNING {key ?? "1"}",
        };
    }

    private static string TargetClause(DmlUpsertStatement statement, DmlDialect dialect) =>
        statement.Constraint is not null && dialect.ProviderType == DbProviderType.PostgreSql
            ? $"ON CONSTRAINT {dialect.Column(statement.Constraint)}"
            : $"({string.Join(", ", statement.Target.Select(c => dialect.Column(c.Column.ColumnName)))})";

    private static string Match(DmlUpsertStatement statement, SqlRenderer renderer, string qualifier = "") =>
        string.Join(
            " AND ",
            statement.Target.Select(c =>
                $"{qualifier}{statement.Dialect.Column(c.Column.ColumnName)} = {renderer.Render(statement.Values[IndexOf(statement, c)])}"
            )
        );

    private static int IndexOf(DmlUpsertStatement statement, DmlColumn column)
    {
        for (var i = 0; i < statement.Columns.Count; i++)
        {
            if (statement.Columns[i] == column)
            {
                return i;
            }
        }

        throw new ArgumentException($"The conflict column {column.Property.Name} is not among the inserted columns.");
    }

    private static HashSet<string> CurrentColumns(SqlNode node, SqlSource current)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Walk(object? n)
        {
            switch (n)
            {
                case SqlColumn column when ReferenceEquals(column.Source, current):
                    names.Add(column.ColumnName);
                    break;
                case SqlNode child:
                    foreach (var property in child.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (property.GetIndexParameters().Length == 0 && property.Name != "EqualityContract")
                        {
                            Walk(property.GetValue(child));
                        }
                    }

                    break;
                case System.Collections.IEnumerable items and not string:
                    foreach (var item in items)
                    {
                        Walk(item);
                    }

                    break;
            }
        }

        Walk(node);
        return names;
    }

    private static List<DmlColumn> ColumnsOf(DmlModel model, LambdaExpression selector)
    {
        var body = selector.Body is UnaryExpression { NodeType: ExpressionType.Convert } convert ? convert.Operand : selector.Body;
        IEnumerable<Expression> members = body is NewExpression create ? create.Arguments : [body];
        return
        [
            .. members.Select(m =>
                m is MemberExpression { Expression: ParameterExpression, Member: PropertyInfo property }
                && model.ColumnsByProperty.TryGetValue(property.Name, out var column)
                    ? column
                    : throw new ArgumentException(
                        $"The conflict columns are named as properties of {model.Type.Name}, such as u => u.Email or "
                            + $"s => new {{ s.Day, s.Page }}, and {m} is not one.",
                        nameof(selector)
                    )
            ),
        ];
    }
}

/// <summary>
/// What an upsert or insert-if-absent writes.
/// </summary>
internal sealed record DmlUpsertStatement
{
    /// <summary>Gets the model.</summary>
    public required DmlModel Model { get; init; }

    /// <summary>Gets the dialect.</summary>
    public required DmlDialect Dialect { get; init; }

    /// <summary>Gets the table, quoted and qualified.</summary>
    public required string Table { get; init; }

    /// <summary>Gets the inserted columns.</summary>
    public required IReadOnlyList<DmlColumn> Columns { get; init; }

    /// <summary>Gets their values, in the same order.</summary>
    public required IReadOnlyList<SqlValue> Values { get; init; }

    /// <summary>Gets the conflict columns, all among the inserted ones.</summary>
    public required IReadOnlyList<DmlColumn> Target { get; init; }

    /// <summary>Gets the named constraint the conflict is on, or null.</summary>
    public string? Constraint { get; init; }

    /// <summary>Gets the assignments over (current, incoming), or null for every other column.</summary>
    public LambdaExpression? Update { get; init; }

    /// <summary>Gets the condition over (current, incoming), or null.</summary>
    public LambdaExpression? UpdateWhere { get; init; }

    /// <summary>Gets the generated key to return, or null.</summary>
    public DmlColumn? ReturnKey { get; init; }

    /// <summary>Gets the columns an update leaves as they are unless its initializer sets them: when and by whom a row
    /// was inserted, its tenant, and its token, which the stamps write.</summary>
    public IReadOnlyCollection<DmlColumn> Unchanging { get; init; } = [];

    /// <summary>
    /// Gets the assignments every update adds, over the current and incoming rows, for each column its initializer
    /// does not set: the update time and user, and the token's next value.
    /// </summary>
    public Func<SqlSource, SqlSource, List<(SqlColumn Column, SqlNode Value)>>? Stamps { get; init; }

    /// <summary>Gets a condition on the current row every update needs, ANDed with its own: its tenant.</summary>
    public Func<SqlSource, SqlNode?>? Guard { get; init; }

    /// <summary>
    /// Finishes an update's assignments and condition with the statement's stamps and guard.
    /// </summary>
    /// <param name="assignments">The update's assignments, which are changed.</param>
    /// <param name="condition">The update's condition, or null.</param>
    /// <param name="current">The current row's source.</param>
    /// <param name="incoming">The incoming row's source.</param>
    /// <returns>The condition with the guard.</returns>
    public SqlNode? Finish(List<(SqlColumn Column, SqlNode Value)> assignments, SqlNode? condition, SqlSource current, SqlSource incoming)
    {
        if (Update is null)
        {
            assignments.RemoveAll(a => Unchanging.Any(c => string.Equals(c.Column.ColumnName, a.Column.ColumnName, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var stamp in Stamps?.Invoke(current, incoming) ?? [])
        {
            if (!assignments.Any(a => string.Equals(a.Column.ColumnName, stamp.Column.ColumnName, StringComparison.OrdinalIgnoreCase)))
            {
                assignments.Add(stamp);
            }
        }

        var guard = Guard?.Invoke(current);
        return (condition, guard) switch
        {
            (null, _) => guard,
            (_, null) => condition,
            _ => new SqlLogical("AND", [condition, guard]),
        };
    }
}
