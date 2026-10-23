// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

#pragma warning disable SA1402 // The scope's state belongs with it.
#pragma warning disable SA1649

using System.Data;
using System.Linq.Expressions;
using Dapper;
using MJCZone.DapperMatic.Dml.Internal;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// The tenant, the user, and which deleted rows a scope reads; and the columns the DML methods fill and filter on
/// their own: <c>[DmSoftDelete]</c>, <c>[DmConcurrencyToken]</c>, <c>[DmCreatedAt]</c>, <c>[DmUpdatedAt]</c>,
/// <c>[DmCreatedBy]</c>, <c>[DmUpdatedBy]</c> and <c>[DmTenantKey]</c>.
/// </summary>
public sealed partial class DmScope
{
    /// <summary>
    /// Reads and writes only a tenant's rows of models with a <c>[DmTenantKey]</c>, and sets it on rows inserted.
    /// </summary>
    /// <param name="tenantId">The tenant's id, converted to each tenant key's type.</param>
    /// <returns>A scope for the tenant.</returns>
    public DmScope ForTenant(object tenantId)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        return new DmScope(Connection, _state with { HasTenant = true, Tenant = tenantId, AllTenants = false });
    }

    /// <summary>
    /// Reads and writes every tenant's rows of models with a <c>[DmTenantKey]</c>, which a scope with no tenant
    /// otherwise refuses: for administration and background work.
    /// </summary>
    /// <returns>A scope for every tenant.</returns>
    public DmScope AllTenants() => new(Connection, _state with { HasTenant = false, Tenant = null, AllTenants = true });

    /// <summary>
    /// Sets the user <c>[DmCreatedBy]</c> and <c>[DmUpdatedBy]</c> record, in place of <see cref="DmDefaults.User"/>.
    /// </summary>
    /// <param name="user">The user, converted to each column's type; null for none, which leaves them as they are.</param>
    /// <returns>A scope for the user.</returns>
    public DmScope WithUser(object? user) => new(Connection, _state with { HasUser = true, User = user });

    /// <summary>
    /// Reads deleted rows of models with a <c>[DmSoftDelete]</c> too, and updates them with the rest.
    /// </summary>
    /// <returns>A scope that sees deleted rows.</returns>
    public DmScope IncludeDeleted() => new(Connection, _state with { Deleted = DmlDeleted.Included });

    /// <summary>
    /// Reads only the deleted rows of the models a statement reads, and every other model's rows that are not deleted.
    /// </summary>
    /// <returns>A scope that sees only deleted rows.</returns>
    public DmScope OnlyDeleted() => new(Connection, _state with { Deleted = DmlDeleted.Only });

    /// <summary>
    /// Removes a row by its key, even of a model with a <c>[DmSoftDelete]</c>; its token checked and its tenant kept.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows removed: 1, or 0 if no row has the key.</returns>
    /// <exception cref="DmConcurrencyException">The row's token is not the one it was read with.</exception>
    public Task<int> HardDeleteAsync<T>(T row, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
        where T : class => HardDeleteAsync(ModelType(row), row, tx, cancellationToken);

    /// <summary>
    /// Removes a row of a model known at run time by its key, even of a model with a <c>[DmSoftDelete]</c>.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows removed: 1, or 0 if no row has the key.</returns>
    /// <exception cref="DmConcurrencyException">The row's token is not the one it was read with.</exception>
    public async Task<int> HardDeleteAsync(Type type, object row, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(row);
        var (model, dialect, table) = Target(type);
        model.RequireWritable("HardDeleteAsync");
        model.RequireKey("HardDeleteAsync");
        return await WriteRowAsync(model, dialect, table, row, DmlRowWrite.HardDelete, tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the row with a key, even of a model with a <c>[DmSoftDelete]</c>.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="key">The key, as <c>DeleteByIdAsync</c> takes it.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows removed: 1, or 0 if no row has the key.</returns>
    public Task<int> HardDeleteByIdAsync<T>(object key, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
        where T : class => HardDeleteByIdAsync(typeof(T), key, tx, cancellationToken);

    /// <summary>
    /// Removes the row of a model known at run time with a key, even of a model with a <c>[DmSoftDelete]</c>.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="key">The key, as <c>DeleteByIdAsync</c> takes it.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows removed: 1, or 0 if no row has the key.</returns>
    public async Task<int> HardDeleteByIdAsync(Type type, object key, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(key);
        var (model, dialect, table) = Target(type);
        model.RequireWritable("HardDeleteByIdAsync");
        model.RequireKey("HardDeleteByIdAsync");
        return await DeleteWhereAsync(model, dialect, table, null, key, hard: true, tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the rows that match a condition, deleted or not, of the scope's tenant.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="where">The condition. To remove every row, use <see cref="HardDeleteAllAsync{T}"/>.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows removed.</returns>
    public async Task<int> HardDeleteAsync<T>(Expression<Func<T, bool>> where, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(where);
        var (model, dialect, table) = Target<T>();
        model.RequireWritable("HardDeleteAsync");
        return await DeleteWhereAsync(model, dialect, table, where, hard: true, tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes every row, deleted or not, of the scope's tenant.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows removed.</returns>
    public Task<int> HardDeleteAllAsync<T>(IDbTransaction? tx = null, CancellationToken cancellationToken = default)
        where T : class => HardDeleteAllAsync(typeof(T), tx, cancellationToken);

    /// <summary>
    /// Removes every row of a model known at run time, deleted or not, of the scope's tenant.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows removed.</returns>
    public async Task<int> HardDeleteAllAsync(Type type, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(type);
        var (model, dialect, table) = Target(type);
        model.RequireWritable("HardDeleteAllAsync");
        return await DeleteWhereAsync(model, dialect, table, null, hard: true, tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the conditions that keep a source's rows to those the scope reads: its tenant's, and not deleted.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="root">Whether the source is the one the statement is about, which OnlyDeleted reads deleted rows of.</param>
    /// <returns>The conditions; none for a model without the columns.</returns>
    internal IEnumerable<SqlNode> FiltersFor(SqlSource source, bool root)
    {
        // Run as a caller, every table read is held to the policies' filter for reading it.
        if (PolicyCondition(source, DmAction.Read) is { } policy)
        {
            yield return policy;
        }

        if (source.Model is not { } model || !model.Behaviours.Any)
        {
            yield break;
        }

        var behaviours = model.Behaviours;
        if (behaviours.TenantKey is { } key && TenantOf(model) is { } tenant)
        {
            yield return new SqlCompare(new SqlColumn(source, key.Column.ColumnName), "=", new SqlValue(key.Property.PropertyType, tenant));
        }

        if (behaviours.SoftDelete is { } deleted)
        {
            var mode = _state.Deleted == DmlDeleted.Only && !root ? DmlDeleted.Excluded : _state.Deleted;
            if (mode != DmlDeleted.Included)
            {
                yield return Deletedness(source, deleted, deleted: mode == DmlDeleted.Only);
            }
        }
    }

    /// <summary>
    /// Finds the tenant a statement reads and writes the rows of, for a model with a tenant key.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The tenant, of the key's type; or null for every tenant, or a model without a tenant key.</returns>
    /// <exception cref="InvalidOperationException">The model has a tenant key and the scope no tenant.</exception>
    internal object? TenantOf(DmlModel model)
    {
        if (model.Behaviours.TenantKey is not { } key || _state.AllTenants)
        {
            return null;
        }

        var tenant = (_state.HasTenant ? _state.Tenant : DmDefaults.TenantId?.Invoke())
            ?? throw new InvalidOperationException(
                $"{model.Type.Name} is kept per tenant ({key.Property.Name}), and the scope has no tenant: give one with "
                    + "ForTenant(id) or DmDefaults.TenantId, or read and write every tenant's rows with AllTenants()."
            );
        return ConvertTo(tenant, key.Property.PropertyType, key.Property.Name);
    }

    /// <summary>
    /// Fills an inserted row's tenant, times, users and token, as the scope and the clock give them.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="row">The row, which is changed.</param>
    /// <exception cref="InvalidOperationException">The row belongs to another tenant than the scope's.</exception>
    internal void PrepareInsert(DmlModel model, object row)
    {
        var behaviours = model.Behaviours;
        if (!behaviours.Any)
        {
            return;
        }

        if (behaviours.TenantKey is { } key && TenantOf(model) is { } tenant)
        {
            var own = key.Property.GetValue(row);
            if (!IsUnset(own) && !Equals(own, tenant))
            {
                throw new InvalidOperationException(
                    $"The {model.Type.Name} row belongs to tenant {own}, and the scope is for tenant {tenant}: rows are written "
                        + "for the scope's tenant only."
                );
            }

            key.Property.SetValue(row, tenant);
        }

        foreach (var time in new[] { behaviours.CreatedAt, behaviours.UpdatedAt }.OfType<DmlColumn>())
        {
            time.Property.SetValue(row, DmlBehaviours.Now(time));
        }

        foreach (var user in new[] { behaviours.CreatedBy, behaviours.UpdatedBy }.OfType<DmlColumn>())
        {
            if (UserFor(user) is { } value)
            {
                user.Property.SetValue(row, value);
            }
        }

        if (behaviours.Token is { } token && IsDefault(token.Property.GetValue(row)))
        {
            token.Property.SetValue(row, behaviours.NextToken(null));
        }
    }

    /// <summary>
    /// Finds the user a column records, of its type.
    /// </summary>
    /// <param name="column">The column.</param>
    /// <returns>The user, or null for none.</returns>
    internal object? UserFor(DmlColumn column)
    {
        var user = _state.HasUser ? _state.User : DmDefaults.User?.Invoke();
        return user is null ? null : ConvertTo(user, column.Property.PropertyType, column.Property.Name);
    }

    /// <summary>
    /// Writes the assignments an update of rows by condition adds: the update time and user, and the token's next
    /// value; none for a column the update sets itself.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="target">The source written.</param>
    /// <param name="dialect">The dialect.</param>
    /// <param name="set">The columns the update sets itself.</param>
    /// <returns>The assignments.</returns>
    internal List<(SqlColumn Column, SqlNode Value)> UpdateAssignments(
        DmlModel model,
        SqlSource target,
        DmlDialect dialect,
        IReadOnlyCollection<string> set
    )
    {
        var behaviours = model.Behaviours;
        var assignments = new List<(SqlColumn, SqlNode)>();
        void Add(DmlColumn? column, Func<DmlColumn, SqlNode?> value)
        {
            if (column is not null && !set.Contains(column.Column.ColumnName, StringComparer.OrdinalIgnoreCase) && value(column) is { } node)
            {
                assignments.Add((new SqlColumn(target, column.Column.ColumnName), node));
            }
        }

        Add(behaviours.UpdatedAt, c => new SqlValue(c.Property.PropertyType, DmlBehaviours.Now(c)));
        Add(behaviours.UpdatedBy, c => UserFor(c) is { } user ? new SqlValue(c.Property.PropertyType, user) : null);
        if (!behaviours.DatabaseWritesToken(dialect))
        {
            Add(
                behaviours.Token,
                c => c.Property.PropertyType == typeof(int) || c.Property.PropertyType == typeof(long)
                    ? new SqlArithmetic(new SqlColumn(target, c.Column.ColumnName), "+", new SqlText("1"))
                    : new SqlValue(c.Property.PropertyType, behaviours.NextToken(null))
            );
        }

        return assignments;
    }

    /// <summary>
    /// Adds to an upsert the columns its update leaves, the stamps it writes, and its tenant guard.
    /// </summary>
    /// <param name="statement">The upsert.</param>
    /// <returns>The upsert, as the model's columns say.</returns>
    internal DmlUpsertStatement WithBehaviours(DmlUpsertStatement statement)
    {
        var model = statement.Model;
        var behaviours = model.Behaviours;
        if (!behaviours.Any)
        {
            return statement;
        }

        var tenant = TenantOf(model);
        var user = behaviours.UpdatedBy is { } updatedBy ? UserFor(updatedBy) : null;
        var databaseToken = behaviours.DatabaseWritesToken(statement.Dialect);
        return statement with
        {
            Unchanging = [.. behaviours.Unchanging, .. behaviours.Token is { } token ? [token] : Array.Empty<DmlColumn>()],
            Stamps = (current, incoming) =>
            {
                var stamps = new List<(SqlColumn Column, SqlNode Value)>();
                if (behaviours.UpdatedAt is { } at)
                {
                    stamps.Add((new SqlColumn(current, at.Column.ColumnName), new SqlColumn(incoming, at.Column.ColumnName)));
                }

                if (behaviours.UpdatedBy is { } by && user is not null)
                {
                    stamps.Add((new SqlColumn(current, by.Column.ColumnName), new SqlColumn(incoming, by.Column.ColumnName)));
                }

                if (behaviours.Token is { } version && !databaseToken)
                {
                    var column = new SqlColumn(current, version.Column.ColumnName);
                    stamps.Add(
                        (
                            column,
                            version.Property.PropertyType == typeof(int) || version.Property.PropertyType == typeof(long)
                                ? new SqlArithmetic(column, "+", new SqlText("1"))
                                : new SqlValue(version.Property.PropertyType, behaviours.NextToken(null))
                        )
                    );
                }

                return stamps;
            },
            Guard = tenant is null || behaviours.TenantKey is not { } key
                ? null
                : current => new SqlCompare(new SqlColumn(current, key.Column.ColumnName), "=", new SqlValue(key.Property.PropertyType, tenant)),
        };
    }

    /// <summary>
    /// Writes the condition that keeps a write by key to the scope's tenant's rows.
    /// </summary>
    /// <param name="source">The source written.</param>
    /// <returns>The condition, or null for a model without a tenant key, or a scope for every tenant.</returns>
    internal SqlNode? TenantFilter(SqlSource source) =>
        source.Model!.Behaviours.TenantKey is { } key && TenantOf(source.Model) is { } tenant
            ? new SqlCompare(new SqlColumn(source, key.Column.ColumnName), "=", new SqlValue(key.Property.PropertyType, tenant))
            : null;

    /// <summary>
    /// Reads back the token SQL Server wrote for a row just inserted; or, after an upsert, whatever token the row has
    /// now, which the update may have changed without the row knowing.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="dialect">The dialect.</param>
    /// <param name="table">The table.</param>
    /// <param name="row">The row, whose token is set if the scope can read a row with its key.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="upserted">Whether the row was upserted, which reads any token, not only one the database writes.</param>
    /// <returns>A task.</returns>
    internal async Task ReadTokenAsync(
        DmlModel model,
        DmlDialect dialect,
        string table,
        object row,
        IDbTransaction? tx,
        CancellationToken cancellationToken,
        bool upserted = false
    )
    {
        if (model.Behaviours.Token is not { } token || model.Keys.Count == 0 || !(upserted || model.Behaviours.DatabaseWritesToken(dialect)))
        {
            return;
        }

        var source = SourceOf(model, table);
        var parameters = new DmlParameters();
        var renderer = new SqlRenderer(dialect, parameters);
        List<SqlNode> conditions = [.. KeyMatch(model, source, (k, _) => k.Property.GetValue(row))];
        if (TenantFilter(source) is { } tenant)
        {
            conditions.Add(tenant);
        }

        var where = string.Join(" AND ", conditions.Select(renderer.Render));
        var sql = $"SELECT {dialect.Column(token.Column.ColumnName)} FROM {table} WHERE {where}";
        var value = await Connection.ExecuteScalarAsync<object>(Command(sql, parameters, tx, cancellationToken)).ConfigureAwait(false);
        if (value is not null and not DBNull)
        {
            token.Property.SetValue(row, DmlValues.Convert(value, token.Property.PropertyType));
        }
    }

    /// <summary>
    /// Updates or deletes one row by its key: its token checked, its tenant kept, and a delete of a model with a
    /// <c>[DmSoftDelete]</c> an update, unless it is hard.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="dialect">The dialect.</param>
    /// <param name="table">The table.</param>
    /// <param name="row">The row, whose stamps and token are set when it is written.</param>
    /// <param name="write">What to write.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="matchBy">The columns the row is found by: null for its key; another unique set for an upsert.</param>
    /// <returns>The number of rows written.</returns>
    /// <exception cref="DmConcurrencyException">The row's token is not the one it was read with.</exception>
    internal async Task<int> WriteRowAsync(
        DmlModel model,
        DmlDialect dialect,
        string table,
        object row,
        DmlRowWrite write,
        IDbTransaction? tx,
        CancellationToken cancellationToken,
        IReadOnlyList<DmlColumn>? matchBy = null
    )
    {
        var behaviours = model.Behaviours;
        var source = SourceOf(model, table);
        if (behaviours.TenantKey is { } key && TenantOf(model) is { } tenant && key.Property.GetValue(row) is { } own
            && !IsUnset(own) && !Equals(own, tenant))
        {
            throw new InvalidOperationException(
                $"The {model.Type.Name} row belongs to tenant {own}, and the scope is for tenant {tenant}: rows are written for the "
                    + "scope's tenant only."
            );
        }

        var soft = write == DmlRowWrite.Delete && behaviours.SoftDelete is not null;
        var hard = write != DmlRowWrite.Update && !soft;
        var databaseToken = behaviours.DatabaseWritesToken(dialect);
        var token = behaviours.Token;
        var expected = token?.Property.GetValue(row);
        var next = token is null || databaseToken || hard ? null : behaviours.NextToken(expected);

        // The row's own values, its stamps, and the token's next value.
        var stamps = new Dictionary<DmlColumn, object?>();
        if (!hard)
        {
            if (behaviours.UpdatedAt is { } at)
            {
                stamps[at] = DmlBehaviours.Now(at);
            }

            if (behaviours.UpdatedBy is { } by && UserFor(by) is { } user)
            {
                stamps[by] = user;
            }

            if (soft)
            {
                stamps[behaviours.SoftDelete!] = behaviours.DeletedValue();
            }

            if (next is not null)
            {
                stamps[token!] = next;
            }
        }

        // Run as a caller: the row is written as the policies hold the action, its columns the caller may not write
        // left as they are, and the policies' values written in theirs.
        var action = write == DmlRowWrite.Update ? DmAction.Update : DmAction.Delete;
        var rules = Require(model, action);
        var hidden = rules is null ? null : Hidden(model, action);
        if (rules is not null && write == DmlRowWrite.Update)
        {
            foreach (var (column, value) in Presets(model, rules))
            {
                stamps[column] = value;
            }
        }

        // Nor are those it may not read: a row read back holds NULL in them, which a whole row written back would store.
        // A patch, or an update's assignments, write them.
        var unread = rules is null || write != DmlRowWrite.Update ? null : HiddenFor(model);
        var set = new List<(DmlColumn Column, object? Value)>();
        if (write == DmlRowWrite.Update)
        {
            set.AddRange(
                Written(model, dialect)
                    .Where(c => !model.Keys.Contains(c) && !behaviours.Unchanging.Contains(c) && c != token && !stamps.ContainsKey(c))
                    .Where(c => hidden is null || !hidden.Contains(c.Column.ColumnName))
                    .Where(c => unread is null || !unread.Contains(c.Column.ColumnName))
                    .Select(c => (c, c.Property.GetValue(row)))
            );
        }

        set.AddRange(stamps.Select(p => (p.Key, p.Value)));
        if (!hard && set.Count == 0)
        {
            return 0;
        }

        var where = KeyMatch(matchBy ?? model.Keys, source, (k, _) => k.Property.GetValue(row));
        where.AddRange(new[] { TenantFilter(source) }.OfType<SqlNode>());
        if (token is not null)
        {
            where.Add(expected is null
                ? new SqlIsNull(new SqlColumn(source, token.Column.ColumnName), Negated: false)
                : new SqlCompare(new SqlColumn(source, token.Column.ColumnName), "=", new SqlValue(token.Property.PropertyType, expected)));
        }

        if (soft)
        {
            where.Add(Deletedness(source, behaviours.SoftDelete!, deleted: false));
        }

        // A row the policies hide is not there: comparing its key reads it, as in PostgreSQL.
        where.AddRange(new[] { PolicyCondition(source, action), PolicyCondition(source, DmAction.Read) }.OfType<SqlNode>());

        var parameters = new DmlParameters();
        var values = set.ConvertAll(s => new SqlValue(s.Column.Property.PropertyType, s.Value));
        var renderer = new SqlRenderer(dialect, parameters).For(values, where);
        var condition = string.Join(" AND ", where.Select(renderer.Render));
        var check = write == DmlRowWrite.Update && rules is not null
            ? CheckOver(source, DmAction.Update, set.Select((s, i) => (s.Column.Column.ColumnName, (SqlNode)values[i])).ToDictionary(p => p.ColumnName, p => p.Item2, StringComparer.OrdinalIgnoreCase))
            : null;
        var keyCondition = condition;
        long matched = -1;
        if (check is not null)
        {
            var counted = new DmlParameters();
            var counter = new SqlRenderer(dialect, counted).For(where);
            matched = await Connection
                .ExecuteScalarAsync<long>(Command($"SELECT COUNT(*) FROM {table} WHERE {string.Join(" AND ", where.Select(counter.Render))}", counted, tx, cancellationToken))
                .ConfigureAwait(false);
            condition += " AND " + new SqlRenderer(dialect, parameters).For(check).Render(check);
        }

        var readsToken = databaseToken && !hard;
        // OUTPUT without INTO is refused on a table with triggers: the new token goes through a table variable.
        var sql = hard
            ? $"DELETE FROM {table} WHERE {condition}"
            : (readsToken ? "DECLARE @dm_token TABLE (v varbinary(8)); " : string.Empty)
                + $"UPDATE {table} SET {string.Join(", ", set.Select((s, i) => $"{dialect.Column(s.Column.Column.ColumnName)} = {renderer.Render(values[i])}"))}"
                + (readsToken ? $" OUTPUT INSERTED.{dialect.Column(token!.Column.ColumnName)} INTO @dm_token" : string.Empty)
                + $" WHERE {condition}"
                + (readsToken ? "; SELECT v FROM @dm_token" : string.Empty);

        async Task<int> Run(IDbTransaction? transaction)
        {
            if (!readsToken)
            {
                return await Connection.ExecuteAsync(Command(sql, parameters, transaction, cancellationToken)).ConfigureAwait(false);
            }

            var newToken = await Connection.ExecuteScalarAsync<byte[]>(Command(sql, parameters, transaction, cancellationToken)).ConfigureAwait(false);
            if (newToken is not null)
            {
                token!.Property.SetValue(row, newToken);
            }

            return newToken is null ? 0 : 1;
        }

        var written = await ToldAsync(
                model,
                dialect,
                table,
                sql,
                keyCondition,
                parameters,
                write == DmlRowWrite.Update ? DmChangeAction.Update : DmChangeAction.Delete,
                tx,
                Run,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (written < matched)
        {
            throw new UnauthorizedAccessException($"The {model.Type.Name} row would break the policies' check; it was not written.");
        }

        if (written == 0 && token is not null)
        {
            throw new DmConcurrencyException(
                $"The {model.Type.Name} row was changed or deleted since it was read ({token.Property.Name} is no longer {Show(expected)}), "
                    + "so it was not written: read it again, and write that."
            );
        }

        if (written > 0)
        {
            foreach (var (column, value) in stamps)
            {
                column.Property.SetValue(row, value);
            }
        }

        return written;
    }

    /// <summary>
    /// Deletes rows by condition, or every row: an update of their <c>[DmSoftDelete]</c> unless the delete is hard.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="dialect">The dialect.</param>
    /// <param name="table">The table.</param>
    /// <param name="where">The condition, or null for every row.</param>
    /// <param name="hard">Whether to remove the rows, deleted or not.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    internal async Task<int> DeleteWhereAsync(
        DmlModel model,
        DmlDialect dialect,
        string table,
        LambdaExpression? where,
        bool hard,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    ) => await DeleteWhereAsync(model, dialect, table, where, key: null, hard, tx, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Deletes the row with a key: an update of its <c>[DmSoftDelete]</c> unless the delete is hard.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="dialect">The dialect.</param>
    /// <param name="table">The table.</param>
    /// <param name="where">A condition, or null.</param>
    /// <param name="key">The key, as <c>DeleteByIdAsync</c> takes it, or null for the rows that meet the condition.</param>
    /// <param name="hard">Whether to remove the rows, deleted or not.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    internal async Task<int> DeleteWhereAsync(
        DmlModel model,
        DmlDialect dialect,
        string table,
        LambdaExpression? where,
        object? key,
        bool hard,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        var behaviours = model.Behaviours;
        var soft = !hard && behaviours.SoftDelete is not null;
        var source = SourceOf(model, table);
        var scope = SqlScope.Write(dialect, TableOf, source, FiltersFor, HiddenFor);

        // A hard delete removes deleted rows too: only the tenant is kept.
        List<SqlNode> conditions = soft ? [.. scope.Filters(source)] : [.. new[] { TenantFilter(source) }.OfType<SqlNode>()];
        if (where is not null)
        {
            conditions.Add(DmlTranslator.Condition(where, source, scope));
        }

        // Run as a caller: the rows the policies' filter for deleting lets through; a condition or key reads rows, so
        // those the filter for reading lets through too, as in PostgreSQL (a soft delete's filters read them already).
        if (Require(model, DmAction.Delete) is not null)
        {
            conditions.AddRange(new[] { PolicyCondition(source, DmAction.Delete) }.OfType<SqlNode>());
            if (!soft && (where is not null || key is not null))
            {
                conditions.AddRange(new[] { PolicyCondition(source, DmAction.Read) }.OfType<SqlNode>());
            }
        }

        if (key is not null)
        {
            conditions.AddRange(
                KeyMatch(
                    model,
                    source,
                    (k, i) => ConvertTo(model.Keys.Count == 1 && IsKeyValue(key) ? key : KeyPart(key, k, i), k.Property.PropertyType, k.Property.Name)
                )
            );
        }

        var parameters = new DmlParameters();
        if (!soft)
        {
            var renderer = new SqlRenderer(dialect, parameters).For(conditions);
            var hardWhere = conditions.Count == 0 ? null : string.Join(" AND ", conditions.Select(renderer.Render));
            var sql = $"DELETE FROM {table}" + (hardWhere is null ? string.Empty : $" WHERE {hardWhere}");
            return await ToldAsync(model, dialect, table, sql, hardWhere, parameters, DmChangeAction.Delete, tx, t => Connection.ExecuteAsync(Command(sql, parameters, t, cancellationToken)), cancellationToken)
                .ConfigureAwait(false);
        }

        var deleted = behaviours.SoftDelete!;
        List<(SqlColumn Column, SqlNode Value)> assignments =
        [
            (new SqlColumn(source, deleted.Column.ColumnName), new SqlValue(deleted.Property.PropertyType, behaviours.DeletedValue())),
            .. UpdateAssignments(model, source, dialect, [deleted.Column.ColumnName]),
        ];
        var set = new SqlRenderer(dialect, parameters).For(assignments.ConvertAll(a => a.Value), conditions);
        var softWhere = conditions.Count == 0 ? null : string.Join(" AND ", conditions.Select(set.Render));
        var update = $"UPDATE {table} SET {string.Join(", ", assignments.Select(a => $"{dialect.Column(a.Column.ColumnName)} = {set.Render(a.Value)}"))}"
            + (softWhere is null ? string.Empty : $" WHERE {softWhere}");
        return await ToldAsync(model, dialect, table, update, softWhere, parameters, DmChangeAction.Delete, tx, t => Connection.ExecuteAsync(Command(update, parameters, t, cancellationToken)), cancellationToken)
            .ConfigureAwait(false);
    }

    // The key of a row, or of a key value, as conditions on a source.
    private static List<SqlNode> KeyMatch(DmlModel model, SqlSource source, Func<DmlColumn, int, object?> value) =>
        KeyMatch(model.Keys, source, value);

    // Columns of a row, or of a key value, as conditions on a source.
    private static List<SqlNode> KeyMatch(IReadOnlyList<DmlColumn> columns, SqlSource source, Func<DmlColumn, int, object?> value) =>
        [
            .. columns.Select((k, i) =>
                (SqlNode)new SqlCompare(new SqlColumn(source, k.Column.ColumnName), "=", new SqlValue(k.Property.PropertyType, value(k, i)))
            ),
        ];

    // A key a row does not have yet: its type's default, or an empty string.
    private static bool IsUnset(object? value) => IsDefault(value) || value is string { Length: 0 };

    private static string Show(object? value) => value is byte[] bytes ? Convert.ToHexString(bytes) : value?.ToString() ?? "null";

    // Whether a row is deleted, or not: a bool (NULL counting as not deleted), or a time that is NULL until deleted.
    private static SqlNode Deletedness(SqlSource source, DmlColumn column, bool deleted)
    {
        var node = new SqlColumn(source, column.Column.ColumnName);
        var type = column.Property.PropertyType;
        if (type == typeof(bool) || type == typeof(bool?))
        {
            var test = new SqlCompare(node, "=", new SqlValue(type, deleted));
            return !deleted && type == typeof(bool?) ? new SqlLogical("OR", [test, new SqlIsNull(node, Negated: false)]) : test;
        }

        return new SqlIsNull(node, Negated: deleted);
    }
}

/// <summary>
/// What a scope holds beyond its connection.
/// </summary>
internal sealed record DmlScopeState
{
    /// <summary>Gets the schema used for every model, or null for each model's own.</summary>
    public string? SchemaName { get; init; }

    /// <summary>Gets the tables used for models instead of their own.</summary>
    public IReadOnlyDictionary<Type, string> TableNames { get; init; } = new Dictionary<Type, string>();

    /// <summary>Gets a value indicating whether the scope gives a tenant.</summary>
    public bool HasTenant { get; init; }

    /// <summary>Gets the tenant.</summary>
    public object? Tenant { get; init; }

    /// <summary>Gets a value indicating whether the scope reads and writes every tenant's rows.</summary>
    public bool AllTenants { get; init; }

    /// <summary>Gets a value indicating whether the scope gives a user.</summary>
    public bool HasUser { get; init; }

    /// <summary>Gets the user.</summary>
    public object? User { get; init; }

    /// <summary>Gets which deleted rows the scope reads.</summary>
    public DmlDeleted Deleted { get; init; }

    /// <summary>Gets the caller the scope's statements run as, held to policies; null for trusted code.</summary>
    public DmlCaller? Caller { get; init; }
}

/// <summary>
/// A write of one row by its key.
/// </summary>
internal enum DmlRowWrite
{
    /// <summary>An update of every column but the key and those an update leaves.</summary>
    Update,

    /// <summary>A delete: an update of the row's <c>[DmSoftDelete]</c>, if it has one.</summary>
    Delete,

    /// <summary>A delete that removes the row.</summary>
    HardDelete,
}

/// <summary>
/// Which rows of a model with a <c>[DmSoftDelete]</c> a scope reads.
/// </summary>
internal enum DmlDeleted
{
    /// <summary>Rows that are not deleted.</summary>
    Excluded,

    /// <summary>Every row.</summary>
    Included,

    /// <summary>Deleted rows of the model a statement is about; rows that are not deleted of the others.</summary>
    Only,
}
