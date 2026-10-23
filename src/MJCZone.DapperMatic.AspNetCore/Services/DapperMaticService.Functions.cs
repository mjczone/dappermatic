// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Running the functions an application declares.
/// </summary>
public partial class DapperMaticService
{
    /// <inheritdoc />
    public virtual Task<FunctionResponse> ExecuteFunctionAsync(
        DapperMaticOperation context,
        string datasourceId,
        string functionName,
        JsonElement? arguments,
        CancellationToken cancellationToken = default
    ) => RunFunctionAsync(context, datasourceId, functionName, arguments, writes: null, cancellationToken);

    /// <summary>
    /// Readies a function to run among a front end's writes, before their transaction begins: the change log its writes
    /// are noted in is made first (MySQL commits on DDL).
    /// </summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="functionName">The function.</param>
    /// <param name="connection">The connection the writes are made on.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    internal async Task PrepareFunctionAsync(string datasourceId, string functionName, IDbConnection connection, CancellationToken cancellationToken)
    {
        if (FunctionOf(datasourceId, functionName) is { Writes.Length: > 0 } function
            && _changeLog?.SinkFor(datasourceId, connection.GetDbProviderType()) is { } sink
            && Written(function).Any(t => sink.Wants(t.Schema, t.Table) is not null))
        {
            await _changeLog.EnsureAsync(datasourceId, connection, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs a function: alone, in a transaction of its own when its writes are heard; or among a front end's writes,
    /// in their transaction, what follows a write (caches, subscriptions, the audit) left to their commit.
    /// </summary>
    /// <param name="context">The operation, authorized as running the function.</param>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="functionName">The function.</param>
    /// <param name="arguments">Its arguments, a JSON object by parameter name.</param>
    /// <param name="writes">The writes it runs among, begun, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Its rows.</returns>
    internal async Task<FunctionResponse> RunFunctionAsync(
        DapperMaticOperation context,
        string datasourceId,
        string functionName,
        JsonElement? arguments,
        WriteSession? writes,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        // Authorized before it is looked for: a caller no policy lets run it does not learn whether it exists.
        await AssertPermissionsAsync(context).ConfigureAwait(false);
        var function =
            FunctionOf(datasourceId, functionName)
            ?? throw new KeyNotFoundException($"The datasource '{datasourceId}' has no function '{functionName}'.");
        if (arguments is { } given && given.ValueKind != JsonValueKind.Object && given.ValueKind != JsonValueKind.Null && given.ValueKind != JsonValueKind.Undefined)
        {
            throw new ArgumentException("A function's arguments are a JSON object, by parameter name.");
        }

        var supplied = arguments is { ValueKind: JsonValueKind.Object } body
            ? body.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase)
            : [];
        var unknown = supplied.Keys.FirstOrDefault(k => !function.Parameters.Exists(p => p.Value is null && string.Equals(p.Name, k, StringComparison.OrdinalIgnoreCase)));
        if (unknown is not null)
        {
            throw new ArgumentException($"The function '{function.Name}' takes no argument '{unknown}'.");
        }

        var access = AccessOf(context);
        var parameters = new DynamicParameters();
        foreach (var parameter in function.Parameters)
        {
            object? value;
            if (parameter.Value is { } path)
            {
                value = ContextValue(
                    await access.GetValueAsync(path, cancellationToken).ConfigureAwait(false)
                        ?? throw new UnauthorizedAccessException($"The function '{function.Name}' takes {parameter.Name} from {path}, which this request does not have."),
                    parameter.Type
                );
            }
            else if (supplied.TryGetValue(parameter.Name, out var json) && json.ValueKind != JsonValueKind.Null)
            {
                try
                {
                    value = json.Deserialize(parameter.Type);
                }
                catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or FormatException)
                {
                    throw new ArgumentException($"The argument '{parameter.Name}' of '{function.Name}' is not a {parameter.Type.Name}.", ex);
                }
            }
            else if (supplied.ContainsKey(parameter.Name) && parameter.Nullable)
            {
                value = null;
            }
            else
            {
                value = parameter.Default ?? (parameter.Nullable ? null : throw new ArgumentException($"The function '{function.Name}' needs the argument '{parameter.Name}'."));
            }

            parameters.Add(parameter.Name, value);
        }

        // Among writes, on their connection and in their transaction; else on a connection of its own.
        var own = writes is null ? await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false) : null;
        var connection = own ?? writes!.Writer;
        using (own)
        {
            var provider = connection.GetDbProviderType();
            var sql = FunctionSql(function, provider);
            var max = function.MaxRows ?? _options.QueryLimits.MaxTop;
            var written = Written(function);

            // A function whose writes webhooks or subscriptions hear runs in a transaction, its changes noted in it.
            var sink = written.Count > 0 ? _changeLog?.SinkFor(datasourceId, provider) : null;
            var heard = sink is null ? [] : written.Where(t => sink.Wants(t.Schema, t.Table) is not null).ToList();
            var transaction = writes?.Transaction;
            if (writes is null && heard.Count > 0)
            {
                await _changeLog!.EnsureAsync(datasourceId, connection, cancellationToken).ConfigureAwait(false);
                transaction = connection.BeginTransaction();
            }

            var rows = await DmTelemetry
                .TracedAsync(
                    provider switch
                    {
                        DbProviderType.SqlServer => "mssql",
                        DbProviderType.PostgreSql => "postgresql",
                        DbProviderType.MySql => "mysql",
                        _ => "sqlite",
                    },
                    function.Name,
                    sql,
                    async () =>
                    {
                        // Read one more than allowed, to tell too many from just enough.
                        var read = new List<Dictionary<string, object?>>();
                        await foreach (
                            var row in ((System.Data.Common.DbConnection)connection)
                                .QueryUnbufferedAsync(sql, parameters, (System.Data.Common.DbTransaction?)transaction)
                                .WithCancellation(cancellationToken)
                                .ConfigureAwait(false)
                        )
                        {
                            read.Add(((IDictionary<string, object?>)row).ToDictionary(p => p.Key, p => p.Value is DBNull ? null : p.Value, StringComparer.Ordinal));
                            if (read.Count > max)
                            {
                                break;
                            }
                        }

                        return read;
                    }
                )
                .ConfigureAwait(false);

            using (writes is null ? transaction : null)
            {
                if (rows.Count > max)
                {
                    throw new ArgumentException($"The function '{function.Name}' returned more than {max} rows.");
                }

                foreach (var (schema, table) in heard)
                {
                    await sink!.WrittenAsync(connection, transaction!, new Dml.DmChange(schema, table, Dml.DmChangeAction.Write, [], [], null), cancellationToken).ConfigureAwait(false);
                }

                if (writes is null)
                {
                    transaction?.Commit();
                }
            }

            // What it wrote: the results kept that read those tables are stale, and this server's subscriptions told;
            // among writes, once they are committed.
            async Task StaleAsync(CancellationToken cancellation)
            {
                if (function.Writes.Contains("*") && _resultCache is not null)
                {
                    await _resultCache.InvalidateAsync(datasourceId, cancellation).ConfigureAwait(false);
                }

                if (written.Count > 0)
                {
                    await CommittedAsync(datasourceId, provider, written, cancellation).ConfigureAwait(false);
                }
            }

            var message = $"Ran function '{function.Name}' of datasource '{datasourceId}': {rows.Count} rows";
            if (writes is not null)
            {
                writes.AfterCommit(StaleAsync);
                writes.Audit(context, message);
                return new FunctionResponse(rows);
            }

            await StaleAsync(cancellationToken).ConfigureAwait(false);
            await LogAuditEventAsync(context, true, message).ConfigureAwait(false);
            return new FunctionResponse(rows);
        }
    }

    // The tables a function says it writes, by schema and name; "*" (any) is none of them.
    private static List<(string? Schema, string Table)> Written(DapperMaticFunction function) =>
        [.. function.Writes.Where(w => w != "*").Select(w => w.LastIndexOf('.') is var dot and > 0 ? ((string?)w[..dot], w[(dot + 1)..]) : (null, w))];

    // A value of the request's context, as the parameter's type: a claim's text read as a number, for instance.
    private static object ContextValue(object value, Type type)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        if (target.IsInstanceOfType(value))
        {
            return value;
        }

        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        return target == typeof(Guid) ? Guid.Parse(text)
            : target.IsEnum ? Enum.Parse(target, text, ignoreCase: true)
            : Convert.ChangeType(text, target, System.Globalization.CultureInfo.InvariantCulture);
    }

    // The function declared for a datasource by that name: one for the datasource before one for every datasource.
    private DapperMaticFunction? FunctionOf(string datasourceId, string name) =>
        _options.Functions
            .Where(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
            .Where(f => f.DatasourceId is null || string.Equals(f.DatasourceId, datasourceId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.DatasourceId is null)
            .FirstOrDefault();

    // The statement a function runs: its SQL, or its routine called as the database calls one.
    private static string FunctionSql(DapperMaticFunction function, DbProviderType provider)
    {
        if (function.Parameters.Find(p => !FunctionParameterName().IsMatch(p.Name)) is { } bad)
        {
            throw new InvalidOperationException($"The function '{function.Name}' has a parameter named '{bad.Name}': a name is letters, digits and underscores.");
        }

        if ((function.Sql is null) == (function.Routine is null))
        {
            throw new InvalidOperationException($"The function '{function.Name}' must have either SQL or a routine, not both or neither.");
        }

        if (function.Sql is { } sql)
        {
            return sql;
        }

        var routine = function.Routine!;
        var names = function.Parameters.Select(p => "@" + p.Name).ToList();
        return provider switch
        {
            DbProviderType.PostgreSql => $"SELECT * FROM {routine}({string.Join(", ", names)})",
            DbProviderType.SqlServer => $"EXEC {routine} {string.Join(", ", function.Parameters.Select(p => $"@{p.Name} = @{p.Name}"))}",
            DbProviderType.MySql => $"CALL {routine}({string.Join(", ", names)})",
            _ => throw new InvalidOperationException($"The function '{function.Name}' runs a routine, and SQLite has none: give it SQL instead."),
        };
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex FunctionParameterName();
}
