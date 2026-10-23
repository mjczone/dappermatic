// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Text;
using Dapper;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// Loads rows in bulk through the provider's own path, reached through the driver the application already uses, so
/// the package depends on no driver: PostgreSQL's binary <c>COPY</c>, SQL Server's <c>SqlBulkCopy</c>,
/// MySqlConnector's <c>MySqlBulkCopy</c>, and on SQLite one prepared statement run for each row.
/// </summary>
internal static class DmlBulk
{
    private static readonly string[] WrapperProperties = ["WrappedConnection", "InnerConnection", "Inner", "Connection"];
    private static readonly string[] WrappedTransactionProperties = ["WrappedTransaction", "InnerTransaction", "Inner", "Transaction"];
    private static readonly ConcurrentDictionary<(Type, Type), MethodInfo> CopyWriters = new();

    /// <summary>
    /// Inserts rows into a table in bulk, within the given transaction.
    /// </summary>
    /// <param name="connection">The connection, open.</param>
    /// <param name="dialect">The dialect.</param>
    /// <param name="table">The table, quoted and qualified.</param>
    /// <param name="columns">The columns to insert, each read from its property of every row.</param>
    /// <param name="rows">The rows, read once, as they are loaded.</param>
    /// <param name="options">The options.</param>
    /// <param name="transaction">The transaction the load runs in.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows inserted.</returns>
    public static async Task<long> InsertAsync(
        IDbConnection connection,
        DmlDialect dialect,
        string table,
        IReadOnlyList<DmlColumn> columns,
        IEnumerable<object> rows,
        DmBulkOptions options,
        IDbTransaction transaction,
        CancellationToken cancellationToken
    )
    {
        var driver = DriverOf(connection);
        var driverTransaction = DriverOf(transaction);
        var reader = new DmlRowReader(rows, [.. columns.Select(c => dialect.ColumnName(c.Column.ColumnName))], Values(driver ?? connection, columns));
        string unavailable;

        switch (dialect.ProviderType)
        {
            case DbProviderType.SqlServer when driver?.GetType().Namespace is "Microsoft.Data.SqlClient" or "System.Data.SqlClient":
                return await SqlBulkCopyAsync(driver, driverTransaction, table, reader, options, cancellationToken)
                    .ConfigureAwait(false);

            case DbProviderType.PostgreSql when driver?.GetType().Namespace == "Npgsql":
                return await CopyAsync(connection, driver, transaction, dialect, table, columns, reader, cancellationToken)
                    .ConfigureAwait(false);

            case DbProviderType.MySql when driver?.GetType().Namespace == "MySqlConnector":
                try
                {
                    return await MySqlBulkCopyAsync(driver, driverTransaction, table, reader, options, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception exception) when (reader.RowsRead == 0 && IsLocalInfileRefusal(exception))
                {
                    unavailable =
                        "MySqlConnector's bulk copy needs AllowLoadLocalInfile=true in the connection string and the "
                        + $"server's local_infile enabled: {exception.GetBaseException().Message}";
                }

                break;

            case DbProviderType.Sqlite:
                return Prepared(connection, transaction, table, dialect, columns, rows);

            case DbProviderType.MySql:
                unavailable = "MySql.Data has no bulk copy; MySqlConnector's MySqlBulkCopy is used when the connection is one.";
                break;

            default:
                unavailable =
                    $"{connection.GetType().Name} is not a connection of the provider's own driver, and does not expose "
                    + "one as WrappedConnection, InnerConnection or Inner.";
                break;
        }

        if (options.Fallback == DmBulkFallback.Throw)
        {
            throw new NotSupportedException($"The native bulk path is unavailable: {unavailable}");
        }

        return await ValuesAsync(connection, dialect, table, columns, rows, options.BatchSize, transaction, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Inserts rows with multi-row <c>INSERT ... VALUES</c> statements, each under the provider's limits on
    /// parameters and rows, reading the rows one statement at a time.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="dialect">The dialect.</param>
    /// <param name="table">The table, quoted and qualified.</param>
    /// <param name="columns">The columns.</param>
    /// <param name="rows">The rows.</param>
    /// <param name="batchSize">The most rows a statement inserts, or 0 for the provider's limit.</param>
    /// <param name="transaction">The transaction, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows inserted.</returns>
    public static async Task<long> ValuesAsync(
        IDbConnection connection,
        DmlDialect dialect,
        string table,
        IReadOnlyList<DmlColumn> columns,
        IEnumerable<object> rows,
        int batchSize,
        IDbTransaction? transaction,
        CancellationToken cancellationToken
    )
    {
        var limit = Math.Max(1, Math.Min(dialect.MaxRowsPerInsert, dialect.MaxParameters / Math.Max(1, columns.Count)));
        var perStatement = batchSize > 0 ? Math.Min(batchSize, limit) : limit;
        var columnList = string.Join(", ", columns.Select(c => dialect.Column(c.Column.ColumnName)));
        long inserted = 0;

        foreach (var chunk in rows.Chunk(perStatement))
        {
            var parameters = new DmlParameters();
            var values = new List<string>(chunk.Length);
            for (var r = 0; r < chunk.Length; r++)
            {
                var names = new string[columns.Count];
                for (var c = 0; c < columns.Count; c++)
                {
                    names[c] = $"@r{r}_{c}";
                    parameters.Add($"r{r}_{c}", columns[c].Property.PropertyType, columns[c].Property.GetValue(chunk[r]));
                }

                values.Add($"({string.Join(", ", names)})");
            }

            var sql = $"INSERT INTO {table} ({columnList}) VALUES {string.Join(", ", values)}";
            inserted += await connection
                .ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }

        return inserted;
    }

    /// <summary>
    /// Finds the driver's own connection behind a wrapper, such as a profiling or logging connection, by the
    /// properties wrappers conventionally expose it as.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <returns>The driver's connection, or null if none is found.</returns>
    internal static DbConnection? DriverOf(IDbConnection connection) => Unwrap(connection, WrapperProperties) as DbConnection;

    private static DbTransaction? DriverOf(IDbTransaction? transaction) =>
        transaction is null ? null : Unwrap(transaction, WrappedTransactionProperties) as DbTransaction;

    private static object? Unwrap(object value, string[] properties)
    {
        object? current = value;
        for (var depth = 0; depth < 4 && current is not null; depth++)
        {
            if (current.GetType().Namespace is "Npgsql" or "Microsoft.Data.SqlClient" or "System.Data.SqlClient"
                or "MySqlConnector" or "MySql.Data.MySqlClient" or "Microsoft.Data.Sqlite" or "System.Data.SQLite")
            {
                return current;
            }

            var type = current.GetType();
            current = properties
                .Select(name => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance))
                .FirstOrDefault(p => p is not null && (typeof(IDbConnection).IsAssignableFrom(p.PropertyType) || typeof(IDbTransaction).IsAssignableFrom(p.PropertyType)))
                ?.GetValue(current);
        }

        return null;
    }

    // Each column's value as the driver takes it: bound into a parameter of the driver, through the type's handler,
    // as a statement would bind it, then read back.
    private static Func<object, int, object?> Values(IDbConnection connection, IReadOnlyList<DmlColumn> columns)
    {
        using var command = connection.CreateCommand();
        var parameters = columns.Select(_ => command.CreateParameter()).ToArray();
        return (row, i) =>
        {
            var column = columns[i];
            var parameter = parameters[i];
            DmlParameters.Bind(parameter, column.Property.PropertyType, column.Property.GetValue(row), column.Property.Name);
            return parameter.Value is DBNull ? null : parameter.Value;
        };
    }

    private static void Set(object target, string property, object? value) =>
        target.GetType().GetProperty(property)?.SetValue(target, value);

    private static async Task<object?> AwaitAsync(object? pending)
    {
        switch (pending)
        {
            case Task task:
                await task.ConfigureAwait(false);
                return task.GetType().GetProperty("Result")?.GetValue(task);
            case null:
                return null;
            default:
                // A ValueTask or ValueTask<T>.
                var asTask = pending.GetType().GetMethod("AsTask", Type.EmptyTypes);
                return asTask is null ? pending : await AwaitAsync(asTask.Invoke(pending, null)).ConfigureAwait(false);
        }
    }

    private static int Seconds(TimeSpan? timeout) => timeout is { } t ? (int)Math.Ceiling(t.TotalSeconds) : 0;

    private static async Task<long> SqlBulkCopyAsync(
        DbConnection driver,
        DbTransaction? transaction,
        string table,
        DmlRowReader reader,
        DmBulkOptions options,
        CancellationToken cancellationToken
    )
    {
        var assembly = driver.GetType().Assembly;
        var ns = driver.GetType().Namespace;
        var bulkType = assembly.GetType($"{ns}.SqlBulkCopy", throwOnError: true)!;
        var optionsType = assembly.GetType($"{ns}.SqlBulkCopyOptions", throwOnError: true)!;
        var transactionType = assembly.GetType($"{ns}.SqlTransaction", throwOnError: true)!;

        // CheckConstraints | KeepNulls | FireTriggers: what an INSERT does, and a bulk copy otherwise skips. Without
        // them check and foreign key constraints are not checked (and are marked untrusted), NULLs become defaults, and
        // triggers do not fire.
        var copyOptions = Enum.ToObject(optionsType, 2 | 8 | 16);
        var bulk = bulkType.GetConstructor([driver.GetType(), optionsType, transactionType])!.Invoke([driver, copyOptions, transaction]);
        try
        {
            Set(bulk, "DestinationTableName", table);
            Set(bulk, "EnableStreaming", true);
            Set(bulk, "BulkCopyTimeout", Seconds(options.Timeout));
            if (options.BatchSize > 0)
            {
                Set(bulk, "BatchSize", options.BatchSize);
            }

            var mappings = bulkType.GetProperty("ColumnMappings")!.GetValue(bulk)!;
            var add = mappings.GetType().GetMethod("Add", [typeof(string), typeof(string)])!;
            for (var i = 0; i < reader.FieldCount; i++)
            {
                add.Invoke(mappings, [reader.GetName(i), reader.GetName(i)]);
            }

            await AwaitAsync(
                    bulkType.GetMethod("WriteToServerAsync", [typeof(DbDataReader), typeof(CancellationToken)])!
                        .Invoke(bulk, [reader, cancellationToken])
                )
                .ConfigureAwait(false);
            return reader.RowsRead;
        }
        finally
        {
            (bulk as IDisposable)?.Dispose();
        }
    }

    private static async Task<long> MySqlBulkCopyAsync(
        DbConnection driver,
        DbTransaction? transaction,
        string table,
        DmlRowReader reader,
        DmBulkOptions options,
        CancellationToken cancellationToken
    )
    {
        var assembly = driver.GetType().Assembly;
        var bulkType = assembly.GetType("MySqlConnector.MySqlBulkCopy", throwOnError: true)!;
        var transactionType = assembly.GetType("MySqlConnector.MySqlTransaction", throwOnError: true)!;
        var mappingType = assembly.GetType("MySqlConnector.MySqlBulkCopyColumnMapping", throwOnError: true)!;
        var bulk = bulkType.GetConstructor([driver.GetType(), transactionType])!.Invoke([driver, transaction]);

        Set(bulk, "DestinationTableName", table);
        Set(bulk, "BulkCopyTimeout", Seconds(options.Timeout));
        var mappings = (IList)bulkType.GetProperty("ColumnMappings")!.GetValue(bulk)!;
        var mappingConstructor = mappingType.GetConstructor([typeof(int), typeof(string), typeof(string)])!;
        for (var i = 0; i < reader.FieldCount; i++)
        {
            mappings.Add(mappingConstructor.Invoke([i, reader.GetName(i), null]));
        }

        var result = await AwaitAsync(
                bulkType.GetMethod("WriteToServerAsync", [typeof(IDataReader), typeof(CancellationToken)])!
                    .Invoke(bulk, [reader, cancellationToken])
            )
            .ConfigureAwait(false);
        var inserted = Convert.ToInt64(
            result?.GetType().GetProperty("RowsInserted")?.GetValue(result) ?? reader.RowsRead,
            CultureInfo.InvariantCulture
        );

        // LOAD DATA LOCAL, which MySqlBulkCopy uses, turns errors into warnings, since the server cannot stop the
        // client mid-file: a NULL in a NOT NULL column becomes '', and a duplicate key is skipped. A warning is
        // therefore a failure here, so that the load is all or none, with constraints checked, as INSERT is.
        var warnings = (result?.GetType().GetProperty("Warnings")?.GetValue(result) as IEnumerable)?.Cast<object>().ToList() ?? [];
        if (warnings.Count > 0 || inserted != reader.RowsRead)
        {
            var messages = warnings.Take(3).Select(w => w.GetType().GetProperty("Message")?.GetValue(w) as string ?? w.ToString());
            throw new InvalidOperationException(
                $"MySQL loaded {inserted} of {reader.RowsRead} rows with {warnings.Count} warnings, which LOAD DATA "
                    + $"LOCAL reports in place of errors: {string.Join("; ", messages)}"
            );
        }

        return inserted;
    }

    private static bool IsLocalInfileRefusal(Exception exception)
    {
        var message = exception.GetBaseException().Message;
        return message.Contains("AllowLoadLocalInfile", StringComparison.OrdinalIgnoreCase)
            || message.Contains("local data is disabled", StringComparison.OrdinalIgnoreCase)
            || message.Contains("local_infile", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<long> CopyAsync(
        IDbConnection connection,
        DbConnection driver,
        IDbTransaction transaction,
        DmlDialect dialect,
        string table,
        IReadOnlyList<DmlColumn> columns,
        DmlRowReader reader,
        CancellationToken cancellationToken
    )
    {
        // Binary COPY writes each value as its column's own type, read from the catalog: the model's types match the
        // columns DDL made, but the names Npgsql needs are the database's.
        var types = (
            await connection
                .QueryAsync<(string Name, string Type)>(
                    new CommandDefinition(
                        "SELECT a.attname, format_type(a.atttypid, NULL) FROM pg_attribute a "
                            + "WHERE a.attrelid = CAST(@table AS regclass) AND a.attnum > 0 AND NOT a.attisdropped",
                        new { table },
                        transaction,
                        cancellationToken: cancellationToken
                    )
                )
                .ConfigureAwait(false)
        ).ToDictionary(t => t.Name, t => t.Type, StringComparer.OrdinalIgnoreCase);
        var typeNames = columns.Select(c => types[dialect.ColumnName(c.Column.ColumnName)]).ToArray();

        var copy = $"COPY {table} ({string.Join(", ", columns.Select(c => dialect.Column(c.Column.ColumnName)))}) FROM STDIN (FORMAT BINARY)";
        var importer = await AwaitAsync(
                driver.GetType().GetMethod("BeginBinaryImportAsync", [typeof(string), typeof(CancellationToken)])!
                    .Invoke(driver, [copy, cancellationToken])
            )
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Npgsql did not begin the binary import.");

        var importerType = importer.GetType();
        var startRow = importerType.GetMethod("StartRowAsync", [typeof(CancellationToken)])!;
        var writeNull = importerType.GetMethod("WriteNullAsync", [typeof(CancellationToken)])!;
        var write = importerType
            .GetMethods()
            .Single(m =>
                m is { Name: "WriteAsync", IsGenericMethodDefinition: true }
                && m.GetParameters() is [_, { ParameterType: var name }, { ParameterType: var token }]
                && name == typeof(string)
                && token == typeof(CancellationToken)
            );

        try
        {
            while (reader.Read())
            {
                await AwaitAsync(startRow.Invoke(importer, [cancellationToken])).ConfigureAwait(false);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    var pending = value is null
                        ? writeNull.Invoke(importer, [cancellationToken])
                        : CopyWriters
                            .GetOrAdd((importerType, value.GetType()), key => write.MakeGenericMethod(key.Item2))
                            .Invoke(importer, [value, typeNames[i], cancellationToken]);
                    await AwaitAsync(pending).ConfigureAwait(false);
                }
            }

            var completed = await AwaitAsync(importerType.GetMethod("CompleteAsync", [typeof(CancellationToken)])!.Invoke(importer, [cancellationToken]))
                .ConfigureAwait(false);
            return Convert.ToInt64(completed ?? reader.RowsRead, CultureInfo.InvariantCulture);
        }
        finally
        {
            if (importer is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    // SQLite has no bulk path; one prepared INSERT, run once per row in one transaction, is its fastest way in.
    private static long Prepared(
        IDbConnection connection,
        IDbTransaction transaction,
        string table,
        DmlDialect dialect,
        IReadOnlyList<DmlColumn> columns,
        IEnumerable<object> rows
    )
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var sql = new StringBuilder($"INSERT INTO {table} (");
        sql.Append(string.Join(", ", columns.Select(c => dialect.Column(c.Column.ColumnName)))).Append(") VALUES (");
        sql.Append(string.Join(", ", columns.Select((_, i) => $"@c{i}"))).Append(')');
        command.CommandText = sql.ToString();

        var parameters = new IDbDataParameter[columns.Count];
        for (var i = 0; i < columns.Count; i++)
        {
            parameters[i] = command.CreateParameter();
            parameters[i].ParameterName = $"c{i}";
            command.Parameters.Add(parameters[i]);
        }

        command.Prepare();
        long inserted = 0;
        foreach (var row in rows)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                DmlParameters.Bind(parameters[i], columns[i].Property.PropertyType, columns[i].Property.GetValue(row), columns[i].Property.Name);
            }

            inserted += command.ExecuteNonQuery();
        }

        return inserted;
    }
}
