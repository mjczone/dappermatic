// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Computes a fingerprint of a table model: a SHA-256 hash that changes whenever anything reconciliation acts
/// on changes, and is identical across processes, machines, cultures and runtime versions otherwise.
/// </summary>
/// <remarks>
/// <para>
/// The fingerprint lets a caller skip reconciling a table whose model has not changed since it was last
/// reconciled, at the cost of one stored hash per table. It covers the table's schema, name and previous names,
/// every column in order with all of its facets, the primary key, and the unique, check and default
/// constraints, indexes and foreign keys.
/// </para>
/// <para>
/// A change the fingerprint reports but reconciliation has nothing to do for -- reordering previous names,
/// changing the case of a name, editing the expression of a check constraint that already exists -- only costs
/// one reconcile that finds nothing. The reverse, a model change the fingerprint misses, would silently skip
/// the change, so the fingerprint errs towards including too much.
/// </para>
/// <para>
/// The schema and table name every column, constraint and index carries are left out: they repeat the table's
/// own, which is in the fingerprint, and creating a table overwrites them on the model with normalized names,
/// so including them would make a model's fingerprint change the first time its table is created.
/// </para>
/// <para>
/// Check and default expressions given as <see cref="Func{T, TResult}"/> delegates are recorded only as
/// present or absent, because a delegate cannot be compared. Reconciliation never changes the expression of a
/// constraint that already exists, so this hides nothing it would act on. Models produced through
/// <see cref="DmTableFactory.Configure(Action{Type, DmTable})"/> must be configured identically on every
/// instance that shares a database, or each will see the others' fingerprints as changes.
/// </para>
/// </remarks>
public static class DmTableFingerprint
{
    /// <summary>
    /// The version of the canonical form the fingerprint is computed over. It is part of every fingerprint, so
    /// changing the canonical form and this number together makes every stored fingerprint differ once.
    /// </summary>
    public const int FormatVersion = 1;

    /// <summary>
    /// Computes the fingerprint of a table model.
    /// </summary>
    /// <param name="table">The table model.</param>
    /// <returns>The SHA-256 hash of the table's canonical form, as 64 lowercase hexadecimal characters.</returns>
    public static string Compute(DmTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(GetCanonicalForm(table))));
    }

    /// <summary>
    /// Returns the canonical text the fingerprint is computed over, one line per element.
    /// </summary>
    /// <param name="table">The table model.</param>
    /// <returns>The canonical form.</returns>
    /// <remarks>
    /// Meant for finding out why a fingerprint changed, by comparing two canonical forms. Its layout can change
    /// from one <see cref="FormatVersion"/> to the next.
    /// </remarks>
    public static string GetCanonicalForm(DmTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var lines = new List<string>
        {
            $"dm-table-fingerprint/{FormatVersion.ToString(CultureInfo.InvariantCulture)}",
            new Line("table")
                .Add("schema", table.SchemaName)
                .Add("name", table.TableName)
                .Add("previous", table.PreviousNames)
                .ToString(),
        };

        // Column order is kept: it is the order a missing table is created in.
        lines.AddRange(table.Columns.Select(Describe));

        if (table.PrimaryKeyConstraint is { } pk)
        {
            lines.Add(
                new Line("primary-key")
                    .Add("name", pk.ConstraintName)
                    .Add("columns", pk.Columns)
                    .Add("previous", pk.PreviousNames)
                    .ToString()
            );
        }

        // Every other list has no meaningful order, so each is sorted: by name, so that two canonical forms
        // compare line for line, then by the whole line, so that two elements with one name are in a stable
        // order too.
        lines.AddRange(
            Sorted(
                table.UniqueConstraints,
                uc => uc.ConstraintName,
                uc =>
                    new Line("unique")
                        .Add("name", uc.ConstraintName)
                        .Add("columns", uc.Columns)
                        .Add("previous", uc.PreviousNames)
                        .ToString()
            )
        );

        lines.AddRange(
            Sorted(
                table.CheckConstraints,
                cc => cc.ConstraintName,
                cc =>
                    new Line("check")
                        .Add("name", cc.ConstraintName)
                        .Add("column", cc.ColumnName)
                        .Add("expression", cc.Expression)
                        .Add("previous", cc.PreviousNames)
                        .ToString()
            )
        );

        lines.AddRange(
            Sorted(
                table.DefaultConstraints,
                dc => dc.ConstraintName,
                dc =>
                    new Line("default")
                        .Add("name", dc.ConstraintName)
                        .Add("column", dc.ColumnName)
                        .Add("expression", dc.Expression)
                        .Add("previous", dc.PreviousNames)
                        .ToString()
            )
        );

        lines.AddRange(
            Sorted(
                table.Indexes,
                ix => ix.IndexName,
                ix =>
                    new Line("index")
                        .Add("name", ix.IndexName)
                        .Add("unique", ix.IsUnique)
                        .Add("columns", ix.Columns)
                        .Add("previous", ix.PreviousNames)
                        .ToString()
            )
        );

        lines.AddRange(
            Sorted(
                table.ForeignKeyConstraints,
                fk => fk.ConstraintName,
                fk =>
                    new Line("foreign-key")
                        .Add("name", fk.ConstraintName)
                        .Add("columns", fk.SourceColumns)
                        .Add("references", fk.ReferencedTableName)
                        .Add("referenced-columns", fk.ReferencedColumns)
                        .Add("on-delete", fk.OnDelete.ToString())
                        .Add("on-update", fk.OnUpdate.ToString())
                        .Add("previous", fk.PreviousNames)
                        .ToString()
            )
        );

        return string.Join("\n", lines) + "\n";
    }

    // The .NET type is written with Type.ToString(), never FullName: for generic types such as Nullable<int>,
    // FullName embeds the runtime's assembly version, which would change every fingerprint on every upgrade.
    private static string Describe(DmColumn column) =>
        new Line("column")
            .Add("name", column.ColumnName)
            .Add("dotnet-type", column.DotnetType?.ToString())
            .Add(
                "provider-types",
                column
                    .ProviderDataTypes.Select(p => $"{p.Key}={p.Value}")
                    .Order(StringComparer.Ordinal)
                    .ToList()
            )
            .Add("length", column.Length)
            .Add("precision", column.Precision)
            .Add("scale", column.Scale)
            .Add("nullable", column.IsNullable)
            .Add("primary-key", column.IsPrimaryKey)
            .Add("auto-increment", column.IsAutoIncrement)
            .Add("unique", column.IsUnique)
            .Add("unicode", column.IsUnicode)
            .Add("fixed-length", column.IsFixedLength)
            .Add("indexed", column.IsIndexed)
            .Add("foreign-key", column.IsForeignKey)
            .Add("references", column.ReferencedTableName)
            .Add("referenced-column", column.ReferencedColumnName)
            .Add("on-delete", column.OnDelete?.ToString())
            .Add("on-update", column.OnUpdate?.ToString())
            .AddExpression("check", column.CheckExpression, column.CheckExpressionFunc is not null)
            .AddExpression("default", column.DefaultExpression, column.DefaultExpressionFunc is not null)
            .Add("previous", column.PreviousNames)
            .ToString();

    private static IEnumerable<string> Sorted<T>(IEnumerable<T> elements, Func<T, string> name, Func<T, string> line) =>
        elements
            .Select(e => (Name: name(e), Line: line(e)))
            .OrderBy(e => e.Name, StringComparer.Ordinal)
            .ThenBy(e => e.Line, StringComparer.Ordinal)
            .Select(e => e.Line);

    /// <summary>
    /// One element of the canonical form. Every value is written with its length in front of it, so no value --
    /// whatever characters it holds -- can be mistaken for the separator or for the next value; a missing value
    /// is written as <c>~</c>, which no length-prefixed value can be.
    /// </summary>
    private sealed class Line
    {
        private readonly StringBuilder _text;

        public Line(string kind)
        {
            _text = new StringBuilder(kind);
        }

        public Line Add(string key, string? value)
        {
            _text.Append(' ').Append(key).Append('=');
            AppendValue(value);
            return this;
        }

        public Line Add(string key, int? value) =>
            Add(key, value?.ToString(CultureInfo.InvariantCulture));

        public Line Add(string key, bool value)
        {
            _text.Append(' ').Append(key).Append('=').Append(value ? '1' : '0');
            return this;
        }

        public Line Add(string key, IReadOnlyCollection<string>? values)
        {
            _text.Append(' ').Append(key).Append("=[");
            var first = true;
            foreach (var value in values ?? [])
            {
                if (!first)
                {
                    _text.Append(',');
                }

                AppendValue(value);
                first = false;
            }

            _text.Append(']');
            return this;
        }

        public Line Add(string key, IReadOnlyCollection<DmOrderedColumn>? columns) =>
            Add(key, columns?.Select(c => $"{c.ColumnName} {c.Order}").ToList());

        public Line AddExpression(string key, string? expression, bool hasFunction)
        {
            if (hasFunction)
            {
                // A delegate cannot be compared, only noticed. "(function)" cannot be confused with a
                // length-prefixed value.
                _text.Append(' ').Append(key).Append("=(function)");
                return this;
            }

            return Add(key, expression);
        }

        public override string ToString() => _text.ToString();

        private void AppendValue(string? value)
        {
            if (value is null)
            {
                _text.Append('~');
                return;
            }

            _text.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
        }
    }
}
