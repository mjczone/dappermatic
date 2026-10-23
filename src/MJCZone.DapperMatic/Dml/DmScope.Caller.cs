// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using Dapper;
using MJCZone.DapperMatic.Dml.Internal;
using MJCZone.DapperMatic.Dml.Rest;
using MJCZone.DapperMatic.Providers.PostgreSql;
using MJCZone.DapperMatic.Providers.SqlServer;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// Statements run as a caller, held to the policies as requests to the web API are.
/// </summary>
public sealed partial class DmScope
{
    private static readonly Dictionary<(RestTable Table, string Name), RestRelation> NoRelations = [];

    /// <summary>Gets a value indicating whether the scope's statements run as a caller, held to policies.</summary>
    public bool RunsAsCaller => _state.Caller is not null;

    /// <summary>
    /// Runs the scope's statements as a caller, held to the policies as the web API holds requests: each table read
    /// or written needs a policy granting the action; reads see the rows the policies' filters let through, with
    /// columns the caller may not read left empty; writes are held to the filters, checks, presets and columns; related
    /// tables (relations, includes, joins) are held to their own policies. Without it, statements run as trusted code.
    /// </summary>
    /// <param name="access">The caller: who they are, their tenant and values.</param>
    /// <param name="policies">The policies, such as those of the web API.</param>
    /// <param name="datasourceId">The datasource the policies know the connection by.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A scope like this one, run as the caller.</returns>
    /// <remarks>
    /// The request's values the policies read (<c>@tenant.id</c>, values read with SQL) are read once, now. A policy's
    /// condition may read only columns the model maps.
    /// </remarks>
    /// <exception cref="ArgumentException">A policy cannot work; the message says which and why.</exception>
    public async Task<DmScope> AsCallerAsync(
        DmAccessContext access,
        IEnumerable<DmPolicy> policies,
        string datasourceId,
        CancellationToken cancellationToken = default
    ) => new(Connection, _state with { Caller = await DmlCaller.CreateAsync(access, policies, datasourceId, cancellationToken).ConfigureAwait(false) });

    /// <summary>
    /// Tells of each table the scope's statements are about to write to, as a caller, by schema and name.
    /// </summary>
    /// <param name="writing">What is told; it is told before each write, which may not be committed yet.</param>
    /// <exception cref="InvalidOperationException">The scope does not run as a caller.</exception>
    internal void OnWriting(Action<string?, string> writing) =>
        (_state.Caller ?? throw new InvalidOperationException("Only a scope run as a caller tells of its writes.")).Writing = writing;

    /// <summary>
    /// Makes the source a model's rows are read from or written to: with the columns the caller may not read marked,
    /// when the scope runs as one.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="table">The table, quoted and qualified.</param>
    /// <returns>The source.</returns>
    internal SqlSource SourceOf(DmlModel model, string table) => new(table, "t0", model.Table, model) { Hidden = HiddenFor(model) };

    /// <summary>
    /// Finds the columns of a model the caller may not read, which reads leave empty and conditions may not compare.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The columns, or null for none (or a scope not run as a caller).</returns>
    internal IReadOnlySet<string>? HiddenFor(DmlModel model) => Hidden(model, DmAction.Read);

    /// <summary>
    /// Finds what the policies let the caller do to a model's table, refusing an action no policy grants.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="action">The action.</param>
    /// <returns>The rules, or null when the scope does not run as a caller.</returns>
    /// <exception cref="UnauthorizedAccessException">No policy lets the caller do it.</exception>
    internal DmAccessRules? Require(DmlModel model, DmAction action)
    {
        if (RulesOf(model, action) is not { } rules)
        {
            return null;
        }

        if (!rules.Allowed)
        {
            var verb = action switch
            {
                DmAction.Read => "read",
                DmAction.Create => "insert into",
                DmAction.Update => "update",
                _ => "delete from",
            };
            throw new UnauthorizedAccessException($"No policy lets this caller {verb} {PolicyTarget(model).Table}.");
        }

        return rules;
    }

    /// <summary>
    /// Writes a policy condition over a source's rows: the filter of the rows an action reaches, or the check of the
    /// rows a write leaves.
    /// </summary>
    /// <param name="source">The source, of a model.</param>
    /// <param name="action">The action.</param>
    /// <param name="check">Whether to write the check rather than the filter.</param>
    /// <returns>The condition; null for none, or a scope not run as a caller.</returns>
    /// <exception cref="UnauthorizedAccessException">No policy lets the caller do it.</exception>
    internal SqlNode? PolicyCondition(SqlSource source, DmAction action, bool check = false)
    {
        if (_state.Caller is not { } caller || source.Model is not { } model || Require(model, action) is not { } rules)
        {
            return null;
        }

        if (caller.Condition(check ? rules.Check : rules.Filter) is not { } condition)
        {
            return null;
        }

        try
        {
            var table = RestTable.Of(model.Table, source.QualifiedName);
            return new SqlTrusted(new RestTranslator(DmlDialect.For(Connection), NoRelations, new RestLimits()).Condition(new RestTrusted(condition), table, source));
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(
                $"A policy on {PolicyTarget(model).Table} reads what {model.Type.Name} does not map: {ex.Message} Map the column on the model.",
                ex
            );
        }
    }

    /// <summary>
    /// Finds the values the policies write on a model's rows, for an insert or an update.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="rules">The rules of the write.</param>
    /// <returns>Each column the policies write, with its value.</returns>
    /// <exception cref="InvalidOperationException">A policy writes a column the model does not map.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller does not have a value a policy writes.</exception>
    internal List<(DmlColumn Column, object? Value)> Presets(DmlModel model, DmAccessRules rules)
    {
        var table = PolicyTarget(model).Table;
        var presets = new List<(DmlColumn, object?)>();
        foreach (var (name, preset) in rules.Presets)
        {
            var column = model.Columns.FirstOrDefault(c => string.Equals(c.Column.ColumnName, name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"A policy on {table} writes the column {name}, which {model.Type.Name} does not map.");
            var value = _state.Caller!.Preset(table, name, preset);
            presets.Add((column, ConvertTo(value, column.Property.PropertyType, column.Property.Name)));
        }

        return presets;
    }

    /// <summary>
    /// Finds the columns of a model the caller may not use for an action: not read, compared or written.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="action">The action.</param>
    /// <returns>The columns, or null for none (or a scope not run as a caller).</returns>
    internal IReadOnlySet<string>? Hidden(DmlModel model, DmAction action)
    {
        if (RulesOf(model, action) is not { Allowed: true, Columns: { } allowed })
        {
            return null;
        }

        var hidden = model.Columns.Select(c => c.Column.ColumnName).Where(c => !allowed.Contains(c)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return hidden.Count == 0 ? null : hidden;
    }

    /// <summary>
    /// Refuses a write that names a column the caller may not write: a write by object leaves such columns out on its
    /// own; one that names them, as a patch or an assignment does, is refused.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="action">The write.</param>
    /// <param name="columns">The columns it names.</param>
    /// <exception cref="UnauthorizedAccessException">One is not a column the caller may write.</exception>
    internal void RequireWritable(DmlModel model, DmAction action, IEnumerable<string> columns)
    {
        if (Hidden(model, action) is { } hidden && columns.FirstOrDefault(hidden.Contains) is { } refused)
        {
            throw new UnauthorizedAccessException($"The column {refused} of {PolicyTarget(model).Table} is not one this caller may write.");
        }
    }

    /// <summary>
    /// Writes the policies' check of a write over the rows it leaves: each column it sets read as its new value, the
    /// others as they are.
    /// </summary>
    /// <param name="source">The source written.</param>
    /// <param name="action">The write.</param>
    /// <param name="values">The new value of each column set, by column name.</param>
    /// <returns>The check, or null for none.</returns>
    internal SqlNode? CheckOver(SqlSource source, DmAction action, IReadOnlyDictionary<string, SqlNode> values) =>
        PolicyCondition(source, action, check: true) is { } check
            ? SqlNode.ReplaceColumns(check, c => ReferenceEquals(c.Source, source) && values.TryGetValue(c.ColumnName, out var value) ? value : null)
            : null;

    /// <summary>
    /// Runs an update held to the policies' check: every row the condition reaches must pass it, or nothing is
    /// changed and the update is refused.
    /// </summary>
    /// <param name="model">The model, whose changes are told when heard.</param>
    /// <param name="dialect">The dialect.</param>
    /// <param name="table">The table.</param>
    /// <param name="assignments">The SET list, written.</param>
    /// <param name="condition">The WHERE condition, written, with the policies' filters.</param>
    /// <param name="parameters">The statement's parameters.</param>
    /// <param name="check">The check over the rows the update leaves, or null for none.</param>
    /// <param name="tx">The transaction to use, or null for one of the method's own.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows updated.</returns>
    /// <exception cref="UnauthorizedAccessException">A row would break the check.</exception>
    internal async Task<int> UpdateCheckedAsync(
        DmlModel model,
        DmlDialect dialect,
        string table,
        string assignments,
        string condition,
        DmlParameters parameters,
        SqlNode? check,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        var update = $"UPDATE {table} SET {assignments} WHERE {condition}";
        if (check is null)
        {
            return await ToldAsync(
                    model,
                    dialect,
                    table,
                    update,
                    condition,
                    parameters,
                    DmChangeAction.Update,
                    tx,
                    t => Connection.ExecuteAsync(Command(update, parameters, t, cancellationToken)),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        var checkSql = new SqlRenderer(dialect, parameters).For(check).Render(check);
        return await InTransactionAsync(
                tx,
                needsOne: true,
                async transaction =>
                {
                    var matched = await Connection
                        .ExecuteScalarAsync<long>(Command($"SELECT COUNT(*) FROM {table} WHERE {condition}", parameters, transaction, cancellationToken))
                        .ConfigureAwait(false);
                    var checkedUpdate = $"{update} AND {checkSql}";
                    var updated = await ToldAsync(
                            model,
                            dialect,
                            table,
                            checkedUpdate,
                            condition,
                            parameters,
                            DmChangeAction.Update,
                            transaction,
                            t => Connection.ExecuteAsync(Command(checkedUpdate, parameters, t, cancellationToken)),
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                    return updated < matched
                        ? throw new UnauthorizedAccessException($"The update would leave rows of {table} that break the policies' check; nothing was changed.")
                        : updated;
                }
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Refuses an insert whose rows break the policies' check: each row's values read in it, a column the insert
    /// does not write as NULL.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="dialect">The dialect.</param>
    /// <param name="table">The table.</param>
    /// <param name="rows">The rows.</param>
    /// <param name="columns">The columns the insert writes.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    /// <exception cref="UnauthorizedAccessException">A row breaks the check.</exception>
    /// <exception cref="ArgumentException">The check reads a column the database fills, which the insert does not write.</exception>
    internal async Task RequireInsertCheckAsync(
        DmlModel model,
        DmlDialect dialect,
        string table,
        IReadOnlyList<object> rows,
        IReadOnlyList<DmlColumn> columns,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        var source = SourceOf(model, table);
        if (PolicyCondition(source, DmAction.Create, check: true) is not { } check)
        {
            return;
        }

        // A column the database fills is not NULL in the row inserted, so the check cannot read it as NULL.
        var written = columns.Select(c => c.Column.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        SqlNode.ReplaceColumns(
            check,
            c =>
            {
                if (!written.Contains(c.ColumnName) && model.Columns.FirstOrDefault(m => string.Equals(m.Column.ColumnName, c.ColumnName, StringComparison.OrdinalIgnoreCase)) is { } filled
                    && (filled.Column.IsAutoIncrement || !string.IsNullOrWhiteSpace(filled.Column.DefaultExpression)))
                {
                    throw new ArgumentException($"The policies' check on {PolicyTarget(model).Table} reads {c.ColumnName}, which the database fills when a row does not give it: give its value.");
                }

                return null;
            }
        );

        const int RowsPerCheck = 50;
        for (var start = 0; start < rows.Count; start += RowsPerCheck)
        {
            var parameters = new DmlParameters();
            var checks = rows.Skip(start)
                .Take(RowsPerCheck)
                .Select(row =>
                {
                    var values = columns.ToDictionary(
                        c => c.Column.ColumnName,
                        c => (SqlNode)new SqlValue(c.Property.PropertyType, c.Property.GetValue(row)),
                        StringComparer.OrdinalIgnoreCase
                    );
                    return SqlNode.ReplaceColumns(check, c => ReferenceEquals(c.Source, source) ? values.GetValueOrDefault(c.ColumnName) ?? new SqlText("NULL") : null);
                })
                .ToList();
            var renderer = new SqlRenderer(dialect, parameters).For(checks);
            var sql = $"SELECT CASE WHEN {string.Join(" AND ", checks.Select(c => $"({renderer.Render(c)})"))} THEN 1 ELSE 0 END";
            if (await Connection.ExecuteScalarAsync<int>(Command(sql, parameters, tx, cancellationToken)).ConfigureAwait(false) != 1)
            {
                throw new UnauthorizedAccessException($"A row breaks the policies' check on {PolicyTarget(model).Table}; nothing was inserted.");
            }
        }
    }

    /// <summary>
    /// Readies rows for an insert run as a caller: the policies' values set on them, and the columns written those the
    /// caller may write (a column the policies write among them).
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="dialect">The dialect.</param>
    /// <param name="rows">The rows, which are changed.</param>
    /// <returns>The columns the insert writes.</returns>
    internal List<DmlColumn> PrepareCallerInsert(DmlModel model, DmlDialect dialect, IEnumerable<object> rows)
    {
        var rules = Require(model, DmAction.Create)!;
        var presets = Presets(model, rules);
        foreach (var row in rows)
        {
            foreach (var (column, value) in presets)
            {
                column.Property.SetValue(row, value);
            }
        }

        var hidden = Hidden(model, DmAction.Create);
        return [.. Written(model, dialect).Where(c => hidden is null || !hidden.Contains(c.Column.ColumnName) || rules.Presets.ContainsKey(c.Column.ColumnName))];
    }

    private DmAccessRules? RulesOf(DmlModel model, DmAction action)
    {
        if (_state.Caller is not { } caller)
        {
            return null;
        }

        var (schema, table) = PolicyTarget(model);
        if (action is not DmAction.Read)
        {
            caller.Writing?.Invoke(schema, table);
        }

        return caller.Rules(schema, table, [.. model.Table.Columns.Select(c => c.ColumnName)], action);
    }

    // The schema and table the policies know a model's rows by, in this scope: the schema the scope reads them in, or
    // the provider's default one.
    private (string? Schema, string Table) PolicyTarget(DmlModel model)
    {
        var table = _state.TableNames.TryGetValue(model.Type, out var substituted) ? substituted : model.Name;
        var schema = SchemaFor(model.Type, model);
        if (string.IsNullOrWhiteSpace(schema))
        {
            schema = Connection.GetDbProviderType() switch
            {
                DbProviderType.SqlServer => SqlServerMethods.DefaultSchemaName,
                DbProviderType.PostgreSql => PostgreSqlMethods.DefaultSchemaName,
                _ => null,
            };
        }

        return (schema, table);
    }
}
