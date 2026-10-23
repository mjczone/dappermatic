// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using Dapper;
using MJCZone.DapperMatic.Dml.Internal;

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// The changes a caller's writes make, told in their transactions to what hears them (webhooks, realtime).
/// </summary>
public sealed partial class DmScope
{
    /// <summary>
    /// Tells each change the scope's writes make, as a caller, in the write's transaction: a write given no transaction
    /// then runs in one of its own, so that what is told is committed with it.
    /// </summary>
    /// <param name="changes">What hears them.</param>
    /// <exception cref="InvalidOperationException">The scope does not run as a caller.</exception>
    internal void OnChanges(IDmChangeSink changes) =>
        (_state.Caller ?? throw new InvalidOperationException("Only a scope run as a caller tells of its changes.")).Changes = changes;

    // The columns asked for, besides the key, of a model's changed rows; or null when its changes are not heard.
    private IReadOnlyList<string>? Heard(DmlModel model)
    {
        if (_state.Caller?.Changes is not { } changes)
        {
            return null;
        }

        var (schema, table) = PolicyTarget(model);
        return changes.Wants(schema, table);
    }

    // The columns a change tells of a model's rows: its key, and those asked for that the model maps.
    private static List<DmlColumn> ChangeColumns(DmlModel model, IReadOnlyList<string> wanted) =>
        [
            .. model.Keys
                .Concat(wanted.Select(w => model.Columns.FirstOrDefault(c => string.Equals(c.Column.ColumnName, w, StringComparison.OrdinalIgnoreCase))).OfType<DmlColumn>())
                .Distinct(),
        ];

    // Tells the rows written, by their own values: the key, and the columns asked for.
    private async Task TellRowsAsync(DmlModel model, DmChangeAction action, IEnumerable<object> rows, IDbTransaction? tx, CancellationToken cancellationToken)
    {
        if (Heard(model) is not { } wanted || tx is null)
        {
            return;
        }

        var columns = ChangeColumns(model, wanted);
        var told = rows.Select(r => (IReadOnlyDictionary<string, object?>)columns.ToDictionary(c => c.Column.ColumnName, c => c.Property.GetValue(r), StringComparer.OrdinalIgnoreCase)).ToList();
        await TellAsync(model, action, told, told.Count, tx, cancellationToken).ConfigureAwait(false);
    }

    private Task TellAsync(DmlModel model, DmChangeAction action, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, int count, IDbTransaction tx, CancellationToken cancellationToken)
    {
        var (schema, table) = PolicyTarget(model);
        return _state.Caller!.Changes!.WrittenAsync(Connection, tx, new DmChange(schema, table, action, [.. model.Keys.Select(k => k.Column.ColumnName)], rows, count), cancellationToken);
    }

    // Runs an UPDATE or DELETE and tells the rows it changed, when the model's changes are heard: PostgreSQL returns
    // their keys from the statement; the others read them first, locked, in the write's transaction (one of its own when
    // none is given), so that the statement changes those rows and no others.
    private async Task<int> ToldAsync(
        DmlModel model,
        DmlDialect dialect,
        string table,
        string sql,
        string? condition,
        DmlParameters parameters,
        DmChangeAction action,
        IDbTransaction? tx,
        Func<IDbTransaction?, Task<int>> run,
        CancellationToken cancellationToken
    )
    {
        if (Heard(model) is not { } wanted)
        {
            return await run(tx).ConfigureAwait(false);
        }

        var columns = ChangeColumns(model, wanted);
        var list = columns.Count == 0 ? "1 AS dm_row" : string.Join(", ", columns.Select(c => dialect.Column(c.Column.ColumnName)));
        return await InTransactionAsync(
                tx,
                needsOne: true,
                async transaction =>
                {
                    async Task<List<IReadOnlyDictionary<string, object?>>> Read(string text) =>
                        [
                            .. (await Connection.QueryAsync(Command(text, parameters, transaction, cancellationToken)).ConfigureAwait(false))
                                .Select(r => (IReadOnlyDictionary<string, object?>)((IDictionary<string, object?>)r)
                                    .Where(p => p.Key != "dm_row")
                                    .ToDictionary(p => p.Key, p => p.Value is DBNull ? null : p.Value, StringComparer.OrdinalIgnoreCase)),
                        ];

                    List<IReadOnlyDictionary<string, object?>> rows;
                    int count;
                    if (dialect.ProviderType == DbProviderType.PostgreSql)
                    {
                        rows = await Read($"{sql} RETURNING {list}").ConfigureAwait(false);
                        count = rows.Count;
                    }
                    else
                    {
                        var where = condition is null ? string.Empty : $" WHERE {condition}";
                        rows = await Read(
                                dialect.ProviderType switch
                                {
                                    DbProviderType.SqlServer => $"SELECT {list} FROM {table} WITH (UPDLOCK, HOLDLOCK){where}",
                                    DbProviderType.MySql => $"SELECT {list} FROM {table}{where} FOR UPDATE",
                                    _ => $"SELECT {list} FROM {table}{where}",
                                }
                            )
                            .ConfigureAwait(false);
                        count = await run(transaction).ConfigureAwait(false);
                    }

                    await TellAsync(model, action, rows, count, transaction!, cancellationToken).ConfigureAwait(false);
                    return count;
                }
            )
            .ConfigureAwait(false);
    }
}
