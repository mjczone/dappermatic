// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Linq.Expressions;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Dml;
using MJCZone.DapperMatic.Dml.Internal;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// The SQL the DML methods write for an expression, without a database: connections are made but never opened.
/// </summary>
public class DmlTranslationTests
{
    private static readonly IDbConnection Sqlite = new Microsoft.Data.Sqlite.SqliteConnection();
    private static readonly IDbConnection SqlServer = new Microsoft.Data.SqlClient.SqlConnection();
    private static readonly IDbConnection MySql = new MySqlConnector.MySqlConnection();
    private static readonly IDbConnection PostgreSql = new Npgsql.NpgsqlConnection();

    public enum Status
    {
        Open,
        Shipped,
    }

    [Fact]
    public void Comparisons_with_a_nullable_column_keep_their_meaning_in_csharp()
    {
        // In C#, a NULL note is != "x"; SQL's <> alone would drop the row.
        Assert.Equal("(\"note\" <> @p0 OR \"note\" IS NULL)", Sql(o => o.Note != "x"));
        Assert.Equal("\"name\" <> @p0", Sql(o => o.Name != "x"));
        Assert.Equal("\"note\" = @p0", Sql(o => o.Note == "x"));

        // Ordered comparisons are false for a NULL in C#, so true once negated. (SQLite gets a decimal as text, so it
        // is cast back for comparing with anything but a column.)
        Assert.Equal("\"total\" > CAST(@p0 AS NUMERIC)", Sql(o => o.Total > 5));
        Assert.Equal("[total] > @p0", Translate(SqlServer, o => o.Total > 5).Sql);
        Assert.Equal("(\"total\" <= CAST(@p0 AS NUMERIC) OR \"total\" IS NULL)", Sql(o => !(o.Total > 5)));
        Assert.Equal("\"qty\" <= @p0", Sql(o => !(o.Qty > 5)));
        decimal? none = null;
        Assert.Equal("1 = 0", Sql(o => o.Total > none));

        // Negations pushed through && and || to the comparisons.
        Assert.Equal("((\"note\" <> @p0 OR \"note\" IS NULL) OR \"qty\" <> @p1)", Sql(o => !(o.Note == "x" && o.Qty == 1)));
        Assert.Equal("\"qty\" = @p0", Sql(o => !!(o.Qty == 1)));
    }

    [Fact]
    public void Two_columns_compare_as_they_would_in_csharp()
    {
        Assert.Equal("\"note\" = \"name\"", Sql(o => o.Note == o.Name));
        Assert.Equal("(\"note\" <> \"name\" OR \"note\" IS NULL)", Sql(o => o.Note != o.Name));
        Assert.Equal(
            "(\"note\" = \"comment\" OR (\"note\" IS NULL AND \"comment\" IS NULL))",
            Sql(o => o.Note == o.Comment)
        );
        Assert.Equal(
            "(\"note\" <> \"comment\" OR (\"note\" IS NULL AND \"comment\" IS NOT NULL) OR (\"note\" IS NOT NULL AND \"comment\" IS NULL))",
            Sql(o => o.Note != o.Comment)
        );
    }

    [Fact]
    public void Nulls_are_tested_with_is_null_even_from_a_variable()
    {
        string? none = null;
        Assert.Equal("\"note\" IS NULL", Sql(o => o.Note == null));
        Assert.Equal("\"note\" IS NOT NULL", Sql(o => o.Note != null));
        Assert.Equal("\"note\" IS NULL", Sql(o => o.Note == none));
        Assert.Equal("\"note\" IS NOT NULL", Sql(o => !(o.Note == none)));
        Assert.Equal("\"total\" IS NOT NULL", Sql(o => o.Total.HasValue));
        Assert.Equal("\"total\" IS NULL", Sql(o => !o.Total.HasValue));
    }

    [Fact]
    public void Values_are_parameters_bound_by_the_column_type()
    {
        var (sql, values) = Translate(Sqlite, o => o.Status == Status.Shipped && 5 < o.Qty && o.Code == 'A');
        Assert.Equal("((\"status\" = @p0 AND \"qty\" > @p1) AND \"code\" = @p2)", sql);

        // The enum and the char, not the integers C# compares them as.
        Assert.Equal([Status.Shipped, 5, 'A'], values);

        var since = new DateTime(2026, 1, 1);
        var filter = new { Min = 3 };
        (sql, values) = Translate(Sqlite, o => o.Qty >= filter.Min && o.CreatedAt > since.AddDays(1));
        Assert.Equal("(\"qty\" >= @p0 AND \"created_at\" > @p1)", sql);
        Assert.Equal([3, new DateTime(2026, 1, 2)], values);
    }

    [Fact]
    public void Bool_columns_and_constants()
    {
        var (sql, values) = Translate(Sqlite, o => o.Active);
        Assert.Equal("\"active\" = @p0", sql);
        Assert.Equal([true], values);

        (_, values) = Translate(Sqlite, o => !o.Active);
        Assert.Equal([false], values);

        Assert.Equal("1 = 1", Sql(o => true));
        var off = false;
        Assert.Equal("(1 = 0 OR \"active\" = @p0)", Sql(o => off || o.Active));
    }

    [Fact]
    public void A_list_becomes_an_in_list()
    {
        var list = new List<int> { 1, 2 };
        int[] array = [3];
        Assert.Equal("\"qty\" IN (@p0, @p1)", Sql(o => list.Contains(o.Qty)));

        // From C# 14 this binds to MemoryExtensions.Contains over a span of the array.
        Assert.Equal("\"qty\" IN (@p0)", Sql(o => array.Contains(o.Qty)));
        Assert.Equal("\"qty\" IN (@p0)", Sql(o => Enumerable.Contains(array, o.Qty)));

        // An enum is not IEquatable<T>, so C# 14 binds the overload taking a comparer, with null.
        Status[] statuses = [Status.Shipped];
        Assert.Equal("\"status\" IN (@p0)", Sql(o => statuses.Contains(o.Status)));
        Assert.Throws<DmTranslationException>(() => Sql(o => new[] { "a" }.Contains(o.Name, StringComparer.OrdinalIgnoreCase)));

        Assert.Equal("1 = 0", Sql(o => new List<int>().Contains(o.Qty)));
        Assert.Equal("1 = 1", Sql(o => !new List<int>().Contains(o.Qty)));
        Assert.Equal("\"qty\" NOT IN (@p0, @p1)", Sql(o => !list.Contains(o.Qty)));

        // A NULL value is in the list only if the list holds a null.
        var notes = new[] { "a", null };
        var notesWithoutNull = new[] { "a" };
        Assert.Equal("(\"note\" IN (@p0) OR \"note\" IS NULL)", Sql(o => notes.Contains(o.Note)));
        Assert.Equal("\"note\" NOT IN (@p0)", Sql(o => !notes.Contains(o.Note)));
        Assert.Equal("(\"note\" NOT IN (@p0) OR \"note\" IS NULL)", Sql(o => !notesWithoutNull.Contains(o.Note)));
    }

    [Fact]
    public void Like_patterns_escape_the_value()
    {
        var (sql, values) = Translate(Sqlite, o => o.Name.Contains("50%_"));
        Assert.Equal("\"name\" LIKE @p0 ESCAPE '~'", sql);
        Assert.Equal(["%50~%~_%"], values);

        // An escape character the value does not contain, so it never escapes itself: MySQL 5.7 mismatches that.
        (sql, values) = Translate(Sqlite, o => o.Name.Contains("a~b_"));
        Assert.Equal("\"name\" LIKE @p0 ESCAPE '!'", sql);
        Assert.Equal(["%a~b!_%"], values);

        (sql, values) = Translate(Sqlite, o => o.Name.Contains("~!#^|$"));
        Assert.Equal("\"name\" LIKE @p0 ESCAPE '~'", sql);
        Assert.Equal(["%~~!#^|$%"], values);

        (_, values) = Translate(Sqlite, o => o.Name.StartsWith("[a]"));
        Assert.Equal(["[a]%"], values);

        // A bracket is a wildcard only on SQL Server.
        (sql, values) = Translate(SqlServer, o => o.Name.EndsWith("[a]"));
        Assert.Equal("[name] LIKE @p0 ESCAPE '~'", sql);
        Assert.Equal(["%~[a]"], values);

        (sql, _) = Translate(Sqlite, o => !o.Name.Contains("x"));
        Assert.Equal("\"name\" NOT LIKE @p0 ESCAPE '~'", sql);
    }

    [Fact]
    public void Empty_strings_are_found_exactly_on_every_provider()
    {
        // SQL Server, and MySQL with a PAD SPACE collation, treat '  ' = '' as true.
        Assert.Equal("(\"note\" IS NULL OR \"note\" = '')", Sql(o => string.IsNullOrEmpty(o.Note)));
        Assert.Equal("([note] IS NULL OR DATALENGTH([note]) = 0)", Translate(SqlServer, o => string.IsNullOrEmpty(o.Note)).Sql);
        Assert.Equal("(`note` IS NULL OR CHAR_LENGTH(`note`) = 0)", Translate(MySql, o => string.IsNullOrEmpty(o.Note)).Sql);
        Assert.Equal(
            "(\"note\" IS NOT NULL AND NOT (\"note\" = ''))",
            Translate(PostgreSql, o => !string.IsNullOrEmpty(o.Note)).Sql
        );
    }

    [Fact]
    public void Expressions_that_cannot_be_translated_are_refused()
    {
        var error = Assert.Throws<DmTranslationException>(() => Sql(o => o.Name.GetHashCode() == 1));
        Assert.Contains("GetHashCode", error.Expression, StringComparison.Ordinal);

        Assert.Throws<DmTranslationException>(() => Sql(o => o.Unmapped == 1));
        Assert.Throws<DmTranslationException>(() => Sql(o => o.Name.Contains("x", StringComparison.Ordinal)));
        Assert.Throws<DmTranslationException>(() => Sql(o => o.SeenAt.Year == 2026));
        Assert.Throws<DmTranslationException>(() => Sql(o => o.Name + o.Qty == "x1"));
        Assert.Throws<DmTranslationException>(() => Sql(o => o.Name.Contains(o.Note!)));
        Assert.Throws<DmTranslationException>(() => Sql(o => new[] { 1 }.Any(i => i == o.Qty)));
    }

    [Fact]
    public void Functions_arithmetic_and_dates()
    {
        Assert.Equal("UPPER(\"name\") = @p0", Sql(o => o.Name.ToUpper() == "ADA"));
        Assert.Equal("LENGTH(\"name\") > @p0", Sql(o => o.Name.Length > 3));
        Assert.Equal("(LEN([name] + N'.') - 1) > @p0", Translate(SqlServer, o => o.Name.Length > 3).Sql);
        Assert.Equal("LTRIM(RTRIM([name])) = @p0", Translate(SqlServer, o => o.Name.Trim() == "x").Sql);
        Assert.Equal("TRIM(\"name\") = @p0", Sql(o => o.Name.Trim() == "x"));

        var (sql, values) = Translate(Sqlite, o => o.Name.Substring(1, 2) == "da");
        Assert.Equal("SUBSTR(\"name\", @p0, @p1) = @p2", sql);
        Assert.Equal([2, 2, "da"], values);

        Assert.Equal("((\"qty\" * @p0) + @p1) > @p2", Sql(o => o.Qty * 2 + 1 > 5));
        Assert.Equal("(\"qty\" / @p0) = @p1", Sql(o => o.Qty / 2 == 1));
        Assert.Equal("(`qty` DIV @p0) = @p1", Translate(MySql, o => o.Qty / 2 == 1).Sql);

        // A division C# makes with fractions: of two integer columns, SQL would drop the fraction.
        Assert.Equal("(\"qty\" * 1.0 / \"id\") > @p0", Sql(o => (double)o.Qty / o.Id > 0.5));
        Assert.Equal("(\"qty\" * 1.0 / @p0) > @p1", Sql(o => (double)o.Qty / 2 > 0.5));
        Assert.Equal("([qty] / @p0) > @p1", Translate(SqlServer, o => (double)o.Qty / 2 > 0.5).Sql);

        Assert.Equal("COALESCE(\"note\", @p0) = @p1", Sql(o => (o.Note ?? string.Empty) == string.Empty));
        Assert.Equal(
            "CASE WHEN \"active\" = @p0 THEN \"qty\" ELSE @p1 END > @p2",
            Sql(o => (o.Active ? o.Qty : 0) > 1)
        );

        // Joining a NULL adds nothing, as in C#.
        Assert.Equal("(\"name\" || @p0 || COALESCE(\"note\", '')) = @p1", Sql(o => o.Name + " " + o.Note == "a b"));
        Assert.Equal("CONCAT(`name`, @p0) = @p1", Translate(MySql, o => o.Name + "!" == "a!").Sql);
        Assert.Equal("([name] + @p0) = @p1", Translate(SqlServer, o => o.Name + "!" == "a!").Sql);

        Assert.Equal("CAST(strftime('%Y', \"created_at\") AS INTEGER) = @p0", Sql(o => o.CreatedAt.Year == 2026));
        Assert.Equal("DATEPART(month, [created_at]) = @p0", Translate(SqlServer, o => o.CreatedAt.Month == 10).Sql);
        Assert.Equal("DAY(`created_at`) = @p0", Translate(MySql, o => o.CreatedAt.Day == 20).Sql);
        Assert.Equal(
            "CAST(FLOOR(EXTRACT(SECOND FROM \"created_at\")) AS integer) = @p0",
            Translate(PostgreSql, o => o.CreatedAt.Second == 1).Sql
        );
    }

    [Fact]
    public void Ignoring_case_compares_lowered_values()
    {
        var (sql, values) = Translate(Sqlite, o => o.Name.Contains("AdA", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("LOWER(\"name\") LIKE @p0 ESCAPE '~'", sql);
        Assert.Equal(["%ada%"], values);

        (sql, values) = Translate(Sqlite, o => string.Equals(o.Name, "ADA", StringComparison.InvariantCultureIgnoreCase));
        Assert.Equal("LOWER(\"name\") = @p0", sql);
        Assert.Equal(["ada"], values);

        (sql, _) = Translate(Sqlite, o => o.Name.Equals("ADA", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("LOWER(\"name\") = @p0", sql);
    }

    [Fact]
    public void Queries_compose_into_one_statement()
    {
        var statement = Sqlite.From<Row>()
            .Where(o => o.Qty > 1)
            .Where(o => o.Active)
            .OrderByDescending(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .Skip(20)
            .Take(10)
            .ToSql();

        Assert.EndsWith(
            " FROM \"rows\" WHERE \"qty\" > @p0 AND \"active\" = @p1 ORDER BY \"created_at\" DESC, \"id\" LIMIT 10 OFFSET 20",
            statement.Sql
        );
        Assert.Equal([new("p0", 1), new("p1", (object?)true)], statement.Parameters);

        // LINQ puts NULLs first ascending and last descending; only PostgreSQL needs telling.
        Assert.EndsWith(
            "ORDER BY \"total\" ASC NULLS FIRST, \"note\" DESC NULLS LAST, \"qty\"",
            PostgreSql.From<Row>().OrderBy(o => o.Total).ThenByDescending(o => o.Note).ThenBy(o => o.Qty).ToSql().Sql
        );
        Assert.EndsWith("ORDER BY `total`", MySql.From<Row>().OrderBy(o => o.Total).ToSql().Sql);

        // SQL Server pages only an ordered query, and a distinct one only by what it selects.
        Assert.EndsWith(
            "ORDER BY 1 OFFSET 0 ROWS FETCH NEXT 5 ROWS ONLY",
            SqlServer.From<Row>().Distinct().Take(5).ToSql().Sql
        );

        Assert.Throws<InvalidOperationException>(() => Sqlite.From<Row>().ThenBy(o => o.Id));
        Assert.Throws<InvalidOperationException>(() => Sqlite.From<Row>().Distinct().OrderBy(o => o.Name.ToUpper()).ToSql());
    }

    [Fact]
    public void Projections_read_only_the_columns_they_use()
    {
        Assert.StartsWith(
            "SELECT \"id\" AS \"Id\", \"name\" AS \"Name\" FROM \"rows\"",
            Sqlite.From<Row>().Select(o => new { o.Id, Upper = o.Name.ToUpper() }).ToSql().Sql
        );
        Assert.StartsWith(
            "SELECT DISTINCT \"status\" AS \"Status\" FROM",
            Sqlite.From<Row>().Select(o => o.Status).Distinct().ToSql().Sql
        );

        // A property that is not a column may be computed from others, so every column is read.
        Assert.Contains("\"created_at\"", Sqlite.From<Row>().Select(o => o.Unmapped).ToSql().Sql, StringComparison.Ordinal);

        // The database compares what a distinct query returns, so it can only return columns.
        Assert.Throws<InvalidOperationException>(() => Sqlite.From<Row>().Select(o => o.Name.ToUpper()).Distinct().ToSql());
    }

    [Fact]
    public void Long_lists_on_sql_server_are_one_json_parameter()
    {
        var ids = Enumerable.Range(1, 2_500).ToList();
        var (sql, values) = Translate(SqlServer, o => ids.Contains(o.Qty));
        Assert.Equal("[qty] IN (SELECT value FROM OPENJSON(@p0))", sql);
        Assert.StartsWith("[1,2,3,", Assert.IsType<string>(Assert.Single(values)), StringComparison.Ordinal);

        // Enums as the integers they are stored as; a short list stays a list of parameters.
        var statuses = Enumerable.Repeat(Status.Shipped, 2_500).ToArray();
        (_, values) = Translate(SqlServer, o => statuses.Contains(o.Status));
        Assert.StartsWith("[1,1,", Assert.IsType<string>(Assert.Single(values)), StringComparison.Ordinal);
        Assert.Equal("[qty] IN (@p0, @p1)", Translate(SqlServer, o => new[] { 1, 2 }.Contains(o.Qty)).Sql);

        // Elsewhere the limit is far higher.
        Assert.Equal(2_500, Translate(Sqlite, o => ids.Contains(o.Qty)).Values.Length);
    }

    [Fact]
    public void Upserts_are_written_for_each_provider()
    {
        string Upsert(IDbConnection db, bool ifAbsent = false)
        {
            var model = DmlModel.For(typeof(Stat));
            var dialect = DmlDialect.For(db);
            var (target, constraint) = DmlUpsert.Target(model, (Expression<Func<Stat, object?>>)(s => new { s.Day, s.Page }), null);
            var row = new Stat { Day = new DateOnly(2026, 10, 21), Page = "home", Views = 1, Seen = new DateTime(2026, 10, 21, 9, 0, 0) };
            var statement = DmlUpsert.Statement(model, dialect, dialect.Table(null, model.Name), row, target, constraint) with
            {
                Update = (Expression<Func<Stat, Stat, Stat>>)((current, incoming) =>
                    new Stat { Views = current.Views + incoming.Views, Seen = incoming.Seen }),
                UpdateWhere = (Expression<Func<Stat, Stat, bool>>)((current, incoming) => incoming.Seen > current.Seen),
            };
            return ifAbsent ? DmlUpsert.InsertIfAbsent(statement, new DmlParameters()) : DmlUpsert.Upsert(statement, new DmlParameters());
        }

        Assert.Equal(
            "INSERT INTO \"public\".\"stats\" AS t (\"day\", \"page\", \"views\", \"seen\") VALUES (@p0, @p1, @p2, @p3) "
                + "ON CONFLICT (\"day\", \"page\") DO UPDATE SET \"views\" = (t.\"views\" + excluded.\"views\"), \"seen\" = excluded.\"seen\" "
                + "WHERE excluded.\"seen\" > t.\"seen\" RETURNING \"id\"",
            Upsert(PostgreSql)
        );
        Assert.StartsWith("INSERT INTO \"stats\" AS t (", Upsert(Sqlite), StringComparison.Ordinal);

        // MySQL assigns left to right: the condition's own column (seen) goes last.
        Assert.Equal(
            "INSERT INTO `stats` (`day`, `page`, `views`, `seen`) VALUES (@p0, @p1, @p2, @p3) ON DUPLICATE KEY UPDATE "
                + "`id` = LAST_INSERT_ID(`id`), "
                + "`views` = IF(VALUES(`seen`) > `seen`, (`views` + VALUES(`views`)), `views`), "
                + "`seen` = IF(VALUES(`seen`) > `seen`, VALUES(`seen`), `seen`); SELECT LAST_INSERT_ID()",
            Upsert(MySql)
        );

        // A conditional update never falls through to the insert.
        Assert.Equal(
            "IF EXISTS (SELECT 1 FROM [dbo].[stats] WITH (UPDLOCK, SERIALIZABLE) WHERE [day] = @p0 AND [page] = @p1) "
                + "BEGIN UPDATE [dbo].[stats] SET [views] = ([views] + @p2), [seen] = @p3 WHERE [day] = @p0 AND [page] = @p1 AND @p3 > [seen]; "
                + "SELECT [id] FROM [dbo].[stats] WHERE [day] = @p0 AND [page] = @p1; END "
                + "ELSE BEGIN INSERT INTO [dbo].[stats] ([day], [page], [views], [seen]) VALUES (@p0, @p1, @p2, @p3); "
                + "SELECT CAST(SCOPE_IDENTITY() AS bigint); END",
            Upsert(SqlServer)
        );

        Assert.Equal(
            "INSERT INTO \"public\".\"stats\" (\"day\", \"page\", \"views\", \"seen\") VALUES (@p0, @p1, @p2, @p3) "
                + "ON CONFLICT (\"day\", \"page\") DO NOTHING RETURNING \"id\"",
            Upsert(PostgreSql, ifAbsent: true)
        );
        Assert.Equal(
            "INSERT INTO `stats` (`day`, `page`, `views`, `seen`) SELECT @p0, @p1, @p2, @p3 FROM (SELECT 1 AS one) AS v "
                + "LEFT JOIN `stats` AS e ON e.`day` = @p0 AND e.`page` = @p1 WHERE e.`day` IS NULL; SELECT ROW_COUNT(), LAST_INSERT_ID()",
            Upsert(MySql, ifAbsent: true)
        );
        Assert.Equal(
            "IF NOT EXISTS (SELECT 1 FROM [dbo].[stats] WITH (UPDLOCK, SERIALIZABLE) WHERE [day] = @p0 AND [page] = @p1) "
                + "BEGIN INSERT INTO [dbo].[stats] ([day], [page], [views], [seen]) VALUES (@p0, @p1, @p2, @p3); "
                + "SELECT CAST(SCOPE_IDENTITY() AS bigint); END ELSE SELECT NULL",
            Upsert(SqlServer, ifAbsent: true)
        );
    }

    [Fact]
    public void Staged_rows_are_merged_for_each_provider()
    {
        string Merge(IDbConnection db, string stage)
        {
            var model = DmlModel.For(typeof(Stat));
            var dialect = DmlDialect.For(db);
            var (target, _) = DmlUpsert.Target(model, (Expression<Func<Stat, object?>>)(s => new { s.Day, s.Page }), null);
            var statement = new DmlUpsertStatement
            {
                Model = model,
                Dialect = dialect,
                Table = dialect.Table(null, model.Name),
                Columns = [.. model.Columns.Where(c => c != model.Generated)],
                Values = [],
                Target = target,
                Update = (Expression<Func<Stat, Stat, Stat>>)((current, incoming) => new Stat { Views = current.Views + incoming.Views }),
                UpdateWhere = (Expression<Func<Stat, Stat, bool>>)((current, incoming) => incoming.Seen > current.Seen),
            };
            return DmlUpsert.Merge(statement, stage, new DmlParameters());
        }

        Assert.Equal(
            "INSERT INTO \"public\".\"stats\" AS t (\"day\", \"page\", \"views\", \"seen\") SELECT \"day\", \"page\", \"views\", \"seen\" FROM \"stage\" "
                + "ON CONFLICT (\"day\", \"page\") DO UPDATE SET \"views\" = (t.\"views\" + excluded.\"views\") WHERE excluded.\"seen\" > t.\"seen\"",
            Merge(PostgreSql, "\"stage\"")
        );

        // SQLite reads ON CONFLICT straight after a SELECT as a join's, hence WHERE true.
        Assert.Contains("FROM \"stage\" WHERE true ON CONFLICT", Merge(Sqlite, "\"stage\""), StringComparison.Ordinal);

        // The table's own columns qualified by its name: the stage's have the same names.
        Assert.Equal(
            "INSERT INTO `stats` (`day`, `page`, `views`, `seen`) SELECT `day`, `page`, `views`, `seen` FROM `stage` ON DUPLICATE KEY UPDATE "
                + "`views` = IF(VALUES(`seen`) > `stats`.`seen`, (`stats`.`views` + VALUES(`views`)), `stats`.`views`)",
            Merge(MySql, "`stage`")
        );

        // Update first, then insert what is missing: inserted rows are not updated again.
        Assert.Equal(
            "UPDATE t SET [views] = (t.[views] + s.[views]) FROM [dbo].[stats] AS t WITH (UPDLOCK, SERIALIZABLE) "
                + "INNER JOIN [#stage] AS s ON t.[day] = s.[day] AND t.[page] = s.[page] WHERE s.[seen] > t.[seen]; "
                + "INSERT INTO [dbo].[stats] ([day], [page], [views], [seen]) SELECT s.[day], s.[page], s.[views], s.[seen] "
                + "FROM [#stage] AS s WHERE NOT EXISTS (SELECT 1 FROM [dbo].[stats] AS t WITH (UPDLOCK, SERIALIZABLE) WHERE t.[day] = s.[day] AND t.[page] = s.[page])",
            Merge(SqlServer, "[#stage]")
        );
    }

    [Fact]
    public void Conflict_targets_must_be_unique()
    {
        var model = DmlModel.For(typeof(Stat));
        Assert.Equal("Day,Page", string.Join(",", DmlUpsert.Target(model, null, "uc_stats_day_page").Columns.Select(c => c.Property.Name)));
        Assert.Equal("Id", string.Join(",", DmlUpsert.Target(model, null, null).Columns.Select(c => c.Property.Name)));
        Assert.Throws<ArgumentException>(() => DmlUpsert.Target(model, (Expression<Func<Stat, object?>>)(s => s.Views), null));
        Assert.Throws<ArgumentException>(() => DmlUpsert.Target(model, null, "nope"));
    }

    [Fact]
    public void Updates_set_values_and_columns()
    {
        var parameters = new DmlParameters();
        var dialect = DmlDialect.For(Sqlite);
        var renderer = new SqlRenderer(dialect, parameters);
        var assignments = DmlTranslator.Assignments(
            (Expression<Func<Row, Row>>)(o => new Row { Note = null, Qty = 2, Comment = o.Name }),
            Source(Sqlite),
            dialect
        );

        Assert.Equal(
            ["\"note\" = @p0", "\"qty\" = @p1", "\"comment\" = \"name\""],
            assignments.Select(a => $"{renderer.Render(a.Column)} = {renderer.Render(a.Value)}")
        );
        Assert.Equal([null, 2], parameters.Values);

        Assert.Throws<DmTranslationException>(() =>
            DmlTranslator.Assignments((Expression<Func<Row, Row>>)(o => o), Source(Sqlite), dialect)
        );
    }

    [Fact]
    public void Pages_are_written_for_each_provider()
    {
        Assert.Equal("S ORDER BY k LIMIT 10 OFFSET 20", DmlDialect.For(Sqlite).Page("S", "k", 20, 10));
        Assert.Equal("S LIMIT -1 OFFSET 20", DmlDialect.For(Sqlite).Page("S", null, 20, null));
        Assert.Equal("S LIMIT 18446744073709551615 OFFSET 20", DmlDialect.For(MySql).Page("S", null, 20, null));
        Assert.Equal("S OFFSET 20", DmlDialect.For(PostgreSql).Page("S", null, 20, null));
        Assert.Equal("S ORDER BY k OFFSET 0 ROWS FETCH NEXT 1 ROWS ONLY", DmlDialect.For(SqlServer).Page("S", "k", null, 1));
        Assert.Equal("S ORDER BY (SELECT NULL) OFFSET 5 ROWS", DmlDialect.For(SqlServer).Page("S", null, 5, null));
        Assert.Equal("S ORDER BY k", DmlDialect.For(SqlServer).Page("S", "k", null, null));
    }

    [Fact]
    public void Arithmetic_keeps_the_meaning_it_has_in_csharp()
    {
        // SQLite keeps a whole decimal as an integer, and 10 / 4 is 2 there: a division with a fraction is made one.
        Assert.Equal("(\"total\" * 1.0 / CAST(@p0 AS NUMERIC)) > CAST(@p1 AS NUMERIC)", Amounts(Sqlite, a => a.Total / 4m > 2.2m));
        Assert.Equal("([total] / @p0) > @p1", Amounts(SqlServer, a => a.Total / 4m > 2.2m));

        // A remainder with a fraction: computed on SQLite, whose % works on integers; refused for floating point
        // where the database has no % for it.
        Assert.Equal(
            "(\"total\" - CAST(@p0 AS NUMERIC) * CAST(\"total\" * 1.0 / CAST(@p0 AS NUMERIC) AS INTEGER)) <> CAST(@p1 AS NUMERIC)",
            Amounts(Sqlite, a => a.Total % 1m != 0m)
        );
        Assert.Equal("([total] % @p0) <> @p1", Amounts(SqlServer, a => a.Total % 1m != 0m));
        Assert.Throws<DmTranslationException>(() => Amounts(SqlServer, a => a.Ratio % 1 > 0));
        Assert.Throws<DmTranslationException>(() => Amounts(PostgreSql, a => a.Ratio % 1 > 0));
        Assert.Equal("(`ratio` % @p0) > @p1", Amounts(MySql, a => a.Ratio % 1 > 0));

        // A ulong is a decimal(20, 0) on SQL Server and PostgreSQL, so its quotient is floored.
        Assert.Equal("FLOOR(([big] / @p0)) = @p1", Amounts(SqlServer, a => a.Big / 2 == 2));
        Assert.Equal("(\"big\" / @p0) = @p1", Amounts(Sqlite, a => a.Big / 2 == 2));

        // A cast that drops a fraction is refused; casts that keep the value are not written at all.
        Assert.Throws<DmTranslationException>(() => Amounts(Sqlite, a => (int)a.Total == 5));
        Assert.Throws<DmTranslationException>(() => Amounts(Sqlite, a => (long)a.Ratio > 5));
        Assert.Equal("\"qty\" > @p0", Amounts(Sqlite, a => (double)a.Qty > 1.5));
    }

    [DmTable(tableName: "amounts")]
    public class Amount
    {
        [DmColumn("id", isPrimaryKey: true)]
        public int Id { get; set; }

        [DmColumn("qty")]
        public int Qty { get; set; }

        [DmColumn("total")]
        public decimal Total { get; set; }

        [DmColumn("ratio")]
        public double Ratio { get; set; }

        [DmColumn("big")]
        public ulong Big { get; set; }
    }

    private static string Amounts(IDbConnection db, Expression<Func<Amount, bool>> where)
    {
        var dialect = DmlDialect.For(db);
        var model = DmlModel.For(typeof(Amount));
        var condition = DmlTranslator.Condition(where, new SqlSource(dialect.Table(null, model.Name), "t0", model.Table, model), dialect);
        return new SqlRenderer(dialect, new DmlParameters()).For(condition).Render(condition);
    }

    private static string Sql(Expression<Func<Row, bool>> where) => Translate(Sqlite, where).Sql;

    private static (string Sql, object?[] Values) Translate(IDbConnection db, Expression<Func<Row, bool>> where)
    {
        var parameters = new DmlParameters();
        var dialect = DmlDialect.For(db);
        var condition = DmlTranslator.Condition(where, Source(db), dialect);
        var sql = new SqlRenderer(dialect, parameters).For(condition).Render(condition);
        return (sql, [.. parameters.Values]);
    }

    private static SqlSource Source(IDbConnection db)
    {
        var model = DmlModel.For(typeof(Row));
        return new SqlSource(DmlDialect.For(db).Table(null, model.Name), "t0", model.Table, model);
    }

    [DmTable(tableName: "stats")]
    [DmUniqueConstraint(columnNames: ["day", "page"], constraintName: "uc_stats_day_page")]
    public class Stat
    {
        [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
        public int Id { get; set; }

        [DmColumn("day")]
        public DateOnly Day { get; set; }

        [DmColumn("page", length: 50)]
        public string Page { get; set; } = string.Empty;

        [DmColumn("views")]
        public int Views { get; set; }

        [DmColumn("seen")]
        public DateTime Seen { get; set; }
    }

    [DmTable(tableName: "rows")]
    public class Row
    {
        [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
        public int Id { get; set; }

        [DmColumn("name", length: 50)]
        public string Name { get; set; } = string.Empty;

        [DmColumn("note", length: 50, isNullable: true)]
        public string? Note { get; set; }

        [DmColumn("comment", length: 50, isNullable: true)]
        public string? Comment { get; set; }

        [DmColumn("qty")]
        public int Qty { get; set; }

        [DmColumn("total", isNullable: true)]
        public decimal? Total { get; set; }

        [DmColumn("status")]
        public Status Status { get; set; }

        [DmColumn("code")]
        public char Code { get; set; }

        [DmColumn("active")]
        public bool Active { get; set; }

        [DmColumn("created_at")]
        public DateTime CreatedAt { get; set; }

        [DmColumn("seen_at")]
        public DateTimeOffset SeenAt { get; set; }

        [DmIgnore]
        public int Unmapped { get; set; }
    }
}
