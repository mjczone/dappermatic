// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using Dapper;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Dml.Internal;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// Patches, which set only some columns.
/// </summary>
public sealed partial class DmScope
{
    /// <summary>
    /// Sets the columns a patch has values for, leaving the rest unchanged.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="patch">
    /// Any object whose properties are named as the model's are, such as an <c>OrderPatch</c> with nullable
    /// properties: a null property leaves its column unchanged. A property the model has no column for is an error,
    /// not ignored, so that a misspelled one does not silently update nothing.
    /// </param>
    /// <param name="where">
    /// The rows to patch; or null for the row with the key the patch holds. A patch never changes a key.
    /// </param>
    /// <param name="setNull">
    /// Columns to set to NULL, which a null in the patch cannot say: <c>o =&gt; new { o.Note, o.Discount }</c>.
    /// </param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows patched; 0 without a round trip if the patch sets nothing.</returns>
    /// <exception cref="ArgumentException">
    /// The patch has a property the model has no column for, <paramref name="setNull"/> names a column that cannot be
    /// NULL or one the patch also sets, or there is no <paramref name="where"/> and the patch has no key.
    /// </exception>
    public Task<int> PatchAsync<T>(
        object patch,
        Expression<Func<T, bool>>? where = null,
        Expression<Func<T, object?>>? setNull = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => PatchCoreAsync(typeof(T), patch, where, ColumnsNamedBy(setNull), tx, cancellationToken);

    /// <summary>
    /// Sets the columns a patch has values for in the row with the key the patch holds, leaving the rest unchanged.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="patch">
    /// Any object whose properties are named as the model's are: a null property leaves its column unchanged, and a
    /// property the model has no column for is an error. Its key properties say which row; a patch never changes a key.
    /// </param>
    /// <param name="setNull">The names of the properties whose columns to set to NULL, or null.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows patched: 1, or 0 if no row has the key or the patch sets nothing.</returns>
    public Task<int> PatchAsync(
        Type type,
        object patch,
        IEnumerable<string>? setNull = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        return PatchCoreAsync(type, patch, where: null, [.. setNull ?? []], tx, cancellationToken);
    }

    private static bool IsDefault(object? value) =>
        value is null || (value.GetType().IsValueType && value.Equals(Activator.CreateInstance(value.GetType())));

    private static List<string> ColumnsNamedBy(LambdaExpression? setNull)
    {
        if (setNull is null)
        {
            return [];
        }

        var row = setNull.Parameters[0];
        var body = setNull.Body is UnaryExpression { NodeType: ExpressionType.Convert } convert ? convert.Operand : setNull.Body;
        IEnumerable<Expression> members = body is NewExpression create ? create.Arguments : [body];
        return
        [
            .. members.Select(m =>
                m is MemberExpression { Expression: { } owner, Member: PropertyInfo property } && owner == row
                    ? property.Name
                    : throw new ArgumentException(
                        $"setNull names the properties to clear, such as o => new {{ o.Note, o.Discount }}, and {m} is not one.",
                        nameof(setNull)
                    )
            ),
        ];
    }

    private static DmlColumn? PatchColumn(DmlModel model, PropertyInfo property) =>
        model.ColumnsByProperty.TryGetValue(property.Name, out var column)
            ? column
            : model.Columns.FirstOrDefault(c =>
                string.Equals(c.Column.ColumnName, property.GetCustomAttribute<DmColumnAttribute>()?.ColumnName ?? property.Name, StringComparison.OrdinalIgnoreCase)
            );

    private async Task<int> PatchCoreAsync(
        Type type,
        object patch,
        LambdaExpression? where,
        IReadOnlyList<string> setNull,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(patch);
        var (model, dialect, table) = Target(type);
        model.RequireWritable("PatchAsync");

        var parameters = new DmlParameters();
        string Parameter(DmlColumn column, object value)
        {
            var name = $"p{parameters.Count}";
            parameters.Add(name, column.Property.PropertyType, ConvertTo(value, column.Property.PropertyType, column.Property.Name));
            return dialect.ParameterReference(name, column.Property.PropertyType);
        }

        var assignments = new List<string>();
        var set = new HashSet<DmlColumn>();
        var newValues = new Dictionary<string, SqlNode>(StringComparer.OrdinalIgnoreCase);
        var keys = new Dictionary<DmlColumn, object?>();
        var properties = patch.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<DmIgnoreAttribute>() is null);

        foreach (var property in properties)
        {
            var column = PatchColumn(model, property)
                ?? throw new ArgumentException(
                    $"{patch.GetType().Name}.{property.Name} matches no column of {model.Type.Name}.",
                    nameof(patch)
                );
            var value = property.GetValue(patch);
            if (model.Keys.Contains(column))
            {
                keys[column] = value;
            }
            else if (value is not null)
            {
                assignments.Add($"{dialect.Column(column.Column.ColumnName)} = {Parameter(column, value)}");
                set.Add(column);
                newValues[column.Column.ColumnName] = new SqlValue(column.Property.PropertyType, ConvertTo(value, column.Property.PropertyType, column.Property.Name));
            }
        }

        foreach (var name in setNull)
        {
            if (!model.ColumnsByProperty.TryGetValue(name, out var column))
            {
                throw new ArgumentException($"setNull names {name}, which is not a column of {model.Type.Name}.", nameof(setNull));
            }

            if (!column.Column.IsNullable || model.Keys.Contains(column))
            {
                throw new ArgumentException($"setNull names {name}, whose column cannot be NULL.", nameof(setNull));
            }

            if (set.Contains(column))
            {
                throw new ArgumentException($"setNull names {name}, which the patch also sets.", nameof(setNull));
            }

            assignments.Add($"{dialect.Column(column.Column.ColumnName)} = NULL");
            newValues[column.Column.ColumnName] = new SqlText("NULL");
        }

        // Run as a caller: the columns patched must be ones the caller may write, and the policies write theirs.
        var rules = Require(model, DmAction.Update);
        if (rules is not null)
        {
            RequireWritable(model, DmAction.Update, newValues.Keys);
            foreach (var (column, value) in Presets(model, rules))
            {
                var name = column.Column.ColumnName;
                assignments.RemoveAll(a => a.StartsWith($"{dialect.Column(name)} = ", StringComparison.Ordinal));
                assignments.Add($"{dialect.Column(name)} = {Parameter(column, value!)}");
                newValues[name] = new SqlValue(column.Property.PropertyType, value);
            }
        }

        if (assignments.Count == 0)
        {
            return 0;
        }

        // A patch holding the token is checked against it, as a write of the row is; one without it changes it.
        var behaviours = model.Behaviours;
        var source = SourceOf(model, table);
        var conditions = new List<SqlNode>();
        object? expected = null;
        if (behaviours.Token is { } token && set.Contains(token))
        {
            expected = properties.First(p => PatchColumn(model, p) == token).GetValue(patch);
            assignments.RemoveAll(a => a.StartsWith($"{dialect.Column(token.Column.ColumnName)} = ", StringComparison.Ordinal));
            conditions.Add(new SqlCompare(new SqlColumn(source, token.Column.ColumnName), "=", new SqlValue(token.Property.PropertyType, expected)));
            if (!behaviours.DatabaseWritesToken(dialect))
            {
                assignments.Add($"{dialect.Column(token.Column.ColumnName)} = {Parameter(token, behaviours.NextToken(expected))}");
            }
        }

        if (behaviours.TenantKey is { } tenantKey && set.Contains(tenantKey) && TenantOf(model) is { } tenant
            && !Equals(ConvertTo(properties.First(p => PatchColumn(model, p) == tenantKey).GetValue(patch), tenantKey.Property.PropertyType, tenantKey.Property.Name), tenant))
        {
            throw new InvalidOperationException($"PatchAsync does not move {model.Type.Name} rows to another tenant than the scope's.");
        }

        var scope = SqlScope.Write(dialect, TableOf, source, FiltersFor, HiddenFor);
        var stamps = UpdateAssignments(model, source, dialect, [.. set.Select(c => c.Column.ColumnName)]);
        var stampRenderer = new SqlRenderer(dialect, parameters).For(stamps.ConvertAll(a => a.Value));
        assignments.AddRange(stamps.Select(a => $"{dialect.Column(a.Column.ColumnName)} = {stampRenderer.Render(a.Value)}"));

        // The rows the policies' filters for updating and reading let through.
        conditions.AddRange(new[] { PolicyCondition(source, DmAction.Update) }.OfType<SqlNode>());

        string condition;
        if (where is not null)
        {
            conditions.InsertRange(0, scope.Filters(source));
            conditions.Add(DmlTranslator.Condition(where, source, scope));
            var renderer = new SqlRenderer(dialect, parameters).For(conditions);
            condition = string.Join(" AND ", conditions.Select(renderer.Render));
        }
        else
        {
            model.RequireKey("PatchAsync");
            if (model.Keys.Any(k => !keys.TryGetValue(k, out var value) || value is null))
            {
                throw new ArgumentException(
                    $"PatchAsync needs a condition, or a patch holding {model.Type.Name}'s key "
                        + $"({string.Join(", ", model.Keys.Select(k => k.Property.Name))}) to say which row.",
                    nameof(patch)
                );
            }

            condition = string.Join(
                " AND ",
                model.Keys.Select(k => $"{dialect.Column(k.Column.ColumnName)} = {Parameter(k, keys[k]!)}")
            );
            conditions.AddRange(new[] { TenantFilter(source), PolicyCondition(source, DmAction.Read) }.OfType<SqlNode>());
            var renderer = new SqlRenderer(dialect, parameters).For(conditions);
            condition = string.Join(" AND ", [condition, .. conditions.Select(renderer.Render)]);
        }

        var check = CheckOver(source, DmAction.Update, newValues);
        var patched = await UpdateCheckedAsync(model, dialect, table, string.Join(", ", assignments), condition, parameters, check, tx, cancellationToken)
            .ConfigureAwait(false);
        return patched == 0 && expected is not null
            ? throw new DmConcurrencyException($"The {model.Type.Name} row was changed or deleted since it was read, so it was not patched.")
            : patched;
    }
}
