// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Providers;

namespace MJCZone.DapperMatic;

public static partial class DbConnectionExtensions
{
    #region Reconciliation renames

    /// <summary>
    /// Works out which elements of an existing table are the model's elements under an earlier name, renames
    /// them in the in-memory copy of the table so that the diff sees them as present, and records the renames
    /// the database needs.
    /// </summary>
    /// <param name="expected">The table as the model describes it.</param>
    /// <param name="actual">The table as read from the database. Renamed in place: it is this call's own copy.</param>
    /// <param name="elements">The element kinds being reconciled. A kind's renames are planned only when it is included.</param>
    /// <param name="providerType">The database provider, which decides whether an implied rename is made.</param>
    /// <param name="renames">Receives the renames to make, in the order they must be made.</param>
    /// <param name="blocked">Receives the renames that are ambiguous.</param>
    /// <param name="doNotAdd">Receives the elements that must not be created because they exist under an ambiguous previous name.</param>
    /// <remarks>
    /// <para>
    /// A declared previous name (<see cref="DmColumn.PreviousNames"/> and its kin) is a rename the model asks for.
    /// It is made whenever the element's kind is included, by whatever means the provider has.
    /// </para>
    /// <para>
    /// A generated name -- <c>ix_users_email</c>, <c>ck_users_age</c> -- moves when its table or column is
    /// renamed, and there is no attribute to declare its old form on. Every form it could have had, built from
    /// the table's and the columns' previous names, is an implied previous name. One found in the database over
    /// the same columns is always matched, so the element is neither reported missing nor created twice -- on
    /// the run that renames the table and on every run after. It is renamed only where the provider can do so in
    /// place: rebuilding a SQLite table, or dropping and re-adding a MySQL foreign key, costs more than a
    /// generated name is worth.
    /// </para>
    /// </remarks>
    private static void ResolveRenames(
        DmTable expected,
        DmTable actual,
        DmReconcileElements elements,
        DbProviderType providerType,
        List<DmSchemaRename> renames,
        List<DmSchemaAddition> blocked,
        HashSet<(DmSchemaElementType, string)> doNotAdd
    )
    {
        if (elements.HasFlag(DmReconcileElements.Columns))
        {
            foreach (var column in expected.Columns)
            {
                if (HasColumn(actual, column.ColumnName))
                {
                    continue;
                }

                var found = column
                    .PreviousNames.Where(p =>
                        HasColumn(actual, p) && !expected.Columns.Any(other => IsSameName(other.ColumnName, p))
                    )
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (found.Count == 1)
                {
                    renames.Add(new DmSchemaRename(DmSchemaElementType.Column, found[0], column.ColumnName));
                    RenameColumnInTable(actual, found[0], column.ColumnName);
                }
                else if (found.Count > 1)
                {
                    blocked.Add(
                        new DmSchemaAddition(
                            DmSchemaElementType.Column,
                            column.ColumnName,
                            $"it exists under more than one previous name ({string.Join(", ", found)}); rename one by hand"
                        )
                    );
                    doNotAdd.Add((DmSchemaElementType.Column, column.ColumnName));
                }
            }
        }

        // Every name the table and each column could have had, current first.
        string[] TableNames() => [expected.TableName, .. expected.PreviousNames];
        string[] ColumnNames(string column) =>
            [
                column,
                .. expected.Columns.FirstOrDefault(c => IsSameName(c.ColumnName, column))?.PreviousNames ?? [],
            ];

        // Each earlier form of a generated name, given how to generate it from a table and its columns.
        IEnumerable<string> EarlierForms(string current, string[] columns, Func<string, string[], string> generate)
        {
            if (!IsSameName(current, generate(expected.TableName, columns)))
            {
                return [];
            }

            return TableNames()
                .SelectMany(table => Combinations([.. columns.Select(ColumnNames)]).Select(cols => generate(table, cols)))
                .Where(name => !IsSameName(name, current))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        ResolveNamedRenames(
            DmSchemaElementType.Index,
            DmReconcileElements.Indexes,
            expected.Indexes,
            actual.Indexes,
            ix => ix.IndexName,
            (ix, name) => ix.IndexName = name,
            ix => ix.PreviousNames,
            ix => EarlierForms(ix.IndexName, ColumnsOf(ix.Columns), DbProviderUtils.GenerateIndexName),
            (model, existing) => SameColumnSet(model.Columns, existing.Columns)
        );

        ResolveNamedRenames(
            DmSchemaElementType.UniqueConstraint,
            DmReconcileElements.UniqueConstraints,
            expected.UniqueConstraints,
            actual.UniqueConstraints,
            uc => uc.ConstraintName,
            (uc, name) =>
            {
                // MySQL and MariaDB also report a unique constraint as an index of the same name.
                foreach (var ix in actual.Indexes.Where(i => IsSameName(i.IndexName, uc.ConstraintName)))
                {
                    ix.IndexName = name;
                }

                uc.ConstraintName = name;
            },
            uc => uc.PreviousNames,
            uc =>
                EarlierForms(uc.ConstraintName, ColumnsOf(uc.Columns), DbProviderUtils.GenerateUniqueConstraintName),
            (model, existing) => SameColumnSet(model.Columns, existing.Columns)
        );

        ResolveNamedRenames(
            DmSchemaElementType.CheckConstraint,
            DmReconcileElements.CheckConstraints,
            expected.CheckConstraints,
            actual.CheckConstraints,
            cc => cc.ConstraintName,
            (cc, name) => cc.ConstraintName = name,
            cc => cc.PreviousNames,
            cc =>
                string.IsNullOrWhiteSpace(cc.ColumnName)
                    ? EarlierTableCheckForms(cc.ConstraintName, expected.TableName, TableNames())
                    : EarlierForms(
                        cc.ConstraintName,
                        [cc.ColumnName],
                        (table, cols) => DbProviderUtils.GenerateCheckConstraintName(table, cols[0])
                    ),
            // Check expressions are not comparable, so the name has to do.
            (_, _) => true
        );

        ResolveNamedRenames(
            DmSchemaElementType.ForeignKeyConstraint,
            DmReconcileElements.ForeignKeyConstraints,
            expected.ForeignKeyConstraints,
            actual.ForeignKeyConstraints,
            fk => fk.ConstraintName,
            (fk, name) => fk.ConstraintName = name,
            fk => fk.PreviousNames,
            fk =>
                EarlierForms(
                    fk.ConstraintName,
                    ColumnsOf(fk.SourceColumns),
                    (table, cols) =>
                        DbProviderUtils.GenerateForeignKeyConstraintName(
                            table,
                            cols,
                            fk.ReferencedTableName,
                            ColumnsOf(fk.ReferencedColumns)
                        )
                ),
            (model, existing) =>
                SameColumnSet(model.SourceColumns, existing.SourceColumns)
                && IsSameName(model.ReferencedTableName, existing.ReferencedTableName)
        );

        void ResolveNamedRenames<T>(
            DmSchemaElementType elementType,
            DmReconcileElements kind,
            List<T> expectedItems,
            List<T> actualItems,
            Func<T, string> nameOf,
            Action<T, string> rename,
            Func<T, List<string>> previousNamesOf,
            Func<T, IEnumerable<string>> impliedPreviousNames,
            Func<T, T, bool> sameDefinition
        )
            where T : class
        {
            var planned = elements.HasFlag(kind);

            // An element in the database under this name that no model element claims as its current name.
            T? Unclaimed(string name) =>
                expectedItems.Any(e => IsSameName(nameOf(e), name))
                    ? null
                    : actualItems.FirstOrDefault(a => IsSameName(nameOf(a), name));

            foreach (var item in expectedItems)
            {
                var name = nameOf(item);
                if (actualItems.Any(a => IsSameName(nameOf(a), name)))
                {
                    continue;
                }

                var declared = planned
                    ? previousNamesOf(item)
                        .Where(p => Unclaimed(p) is not null)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                    : [];

                if (declared.Count > 1)
                {
                    blocked.Add(
                        new DmSchemaAddition(
                            elementType,
                            name,
                            $"it exists under more than one previous name ({string.Join(", ", declared)}); rename one by hand"
                        )
                    );
                    doNotAdd.Add((elementType, name));
                    continue;
                }

                if (declared.Count == 1)
                {
                    renames.Add(new DmSchemaRename(elementType, declared[0], name));
                    rename(Unclaimed(declared[0])!, name);
                    continue;
                }

                var implied = impliedPreviousNames(item)
                    .Where(p => Unclaimed(p) is { } existing && sameDefinition(item, existing))
                    .ToList();

                if (implied.Count == 1)
                {
                    if (planned && CanRenameInPlace(providerType, elementType))
                    {
                        renames.Add(new DmSchemaRename(elementType, implied[0], name));
                    }

                    rename(Unclaimed(implied[0])!, name);
                }
            }
        }
    }

    /// <summary>
    /// Returns the earlier forms of a table-level check constraint's generated name, <c>ck_{table}_{n}</c>, under
    /// each of the table's previous names.
    /// </summary>
    private static IEnumerable<string> EarlierTableCheckForms(string current, string tableName, string[] tableNames)
    {
        var prefix = DbProviderUtils.GenerateCheckConstraintName(tableName, "0")[..^1];
        if (!current.StartsWith(prefix, NameComparison))
        {
            return [];
        }

        var suffix = current[prefix.Length..];
        if (!IsSameName(current, DbProviderUtils.GenerateCheckConstraintName(tableName, suffix)))
        {
            return [];
        }

        return tableNames
            .Select(table => DbProviderUtils.GenerateCheckConstraintName(table, suffix))
            .Where(name => !IsSameName(name, current))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns every way of picking one name for each column.
    /// </summary>
    private static IEnumerable<string[]> Combinations(string[][] namesPerColumn)
    {
        IEnumerable<string[]> combinations = [[]];
        foreach (var names in namesPerColumn)
        {
            combinations = combinations.SelectMany(prefix => names.Select(name => (string[])[.. prefix, name]));
        }

        return combinations;
    }

    private static string[] ColumnsOf(IEnumerable<DmOrderedColumn> columns) => [.. columns.Select(c => c.ColumnName)];

    /// <summary>
    /// Renames a column in the in-memory copy of a table, along with every reference to it, as the database
    /// itself does when the column is renamed.
    /// </summary>
    private static void RenameColumnInTable(DmTable table, string oldName, string newName)
    {
        void RenameIn(IEnumerable<DmOrderedColumn> columns)
        {
            foreach (var column in columns.Where(c => IsSameName(c.ColumnName, oldName)))
            {
                column.ColumnName = newName;
            }
        }

        foreach (var column in table.Columns.Where(c => IsSameName(c.ColumnName, oldName)))
        {
            column.ColumnName = newName;
        }

        if (table.PrimaryKeyConstraint is { } pk)
        {
            RenameIn(pk.Columns);
        }

        foreach (var ix in table.Indexes)
        {
            RenameIn(ix.Columns);
        }

        foreach (var uc in table.UniqueConstraints)
        {
            RenameIn(uc.Columns);
        }

        foreach (var fk in table.ForeignKeyConstraints)
        {
            RenameIn(fk.SourceColumns);
        }

        foreach (var cc in table.CheckConstraints.Where(c => c.ColumnName is not null && IsSameName(c.ColumnName, oldName)))
        {
            cc.ColumnName = newName;
        }

        foreach (var dc in table.DefaultConstraints.Where(d => IsSameName(d.ColumnName, oldName)))
        {
            dc.ColumnName = newName;
        }
    }

    /// <summary>
    /// Returns whether a provider renames an element of this kind in place, without rebuilding the table or
    /// replacing the element.
    /// </summary>
    private static bool CanRenameInPlace(DbProviderType providerType, DmSchemaElementType elementType) =>
        providerType switch
        {
            DbProviderType.SqlServer or DbProviderType.PostgreSql => true,
            DbProviderType.MySql => elementType is DmSchemaElementType.Index or DmSchemaElementType.UniqueConstraint,
            DbProviderType.Sqlite => elementType is DmSchemaElementType.Index,
            _ => false,
        };

    private static bool HasColumn(DmTable table, string columnName) =>
        table.Columns.Any(c => IsSameName(c.ColumnName, columnName));

    private static bool IsSameName(string left, string right) => left.Equals(right, NameComparison);

    #endregion // Reconciliation renames
}
