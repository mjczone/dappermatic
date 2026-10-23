// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Globalization;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic;

public static partial class DbConnectionExtensions
{
    #region Reconciliation diff

    private const StringComparison NameComparison = StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Diffs a model table against the table as it exists in the database.
    /// </summary>
    /// <param name="expected">The table as the model describes it.</param>
    /// <param name="actual">The table as read back from the database.</param>
    /// <param name="elements">The element kinds to consider.</param>
    /// <param name="applySafeAlterations">Whether to plan safe alterations of existing columns, rather than report them as drift.</param>
    /// <param name="supportsCheckConstraints">Whether the database server can hold check constraints.</param>
    /// <param name="providerType">The database provider, whose spelling of each column type is restated by an alteration.</param>
    /// <param name="renames">The renames already resolved, which the in-memory <paramref name="actual"/> reflects.</param>
    /// <param name="drift">Drift already found, added to.</param>
    /// <param name="blocked">Blocked elements already found, added to.</param>
    /// <param name="doNotAdd">Elements never to plan as missing, because they exist under an ambiguous previous name.</param>
    /// <returns>The reconciliation plan.</returns>
    /// <remarks>
    /// Drift is reported for every kind of element whatever the filter, which only decides what is planned.
    /// An excluded kind's missing and blocked elements are still worked out, then discarded, so that
    /// <see cref="DmReconcileElements.None"/> reports drift and nothing else.
    /// </remarks>
    private static DmTableReconciliationPlan BuildPlan(
        DmTable expected,
        DmTable actual,
        DmReconcileElements elements,
        bool applySafeAlterations,
        bool supportsCheckConstraints,
        DbProviderType providerType,
        List<DmSchemaRename> renames,
        List<DmSchemaDrift> drift,
        List<DmSchemaAddition> blocked,
        HashSet<(DmSchemaElementType, string)> doNotAdd
    )
    {
        List<DmSchemaAddition> BlockedIfIncluded(DmReconcileElements kind) => elements.HasFlag(kind) ? blocked : [];
        bool MayAdd(DmSchemaElementType type, string name) => !doNotAdd.Contains((type, name));

        var (missingColumns, alterations) = DiffColumns(
            expected,
            actual,
            drift,
            blocked,
            doNotAdd,
            applySafeAlterations,
            providerType
        );

        missingColumns = elements.HasFlag(DmReconcileElements.Columns)
            ? [.. missingColumns.Where(c => MayAdd(DmSchemaElementType.Column, c.ColumnName))]
            : [];

        // Anything that names a column can only be created once that column is there. A filter that
        // excludes columns while the model has grown one would otherwise produce DDL the database
        // rejects, so those elements are reported as blocked rather than attempted.
        var availableColumns = actual.Columns.Select(c => c.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var added in missingColumns)
        {
            availableColumns.Add(added.ColumnName);
        }

        var missingPrimaryKey = DiffPrimaryKey(
            expected,
            actual,
            drift,
            availableColumns,
            BlockedIfIncluded(DmReconcileElements.PrimaryKey)
        );

        var missingUniqueConstraints = DiffUniqueConstraints(
            expected,
            actual,
            drift,
            availableColumns,
            BlockedIfIncluded(DmReconcileElements.UniqueConstraints)
        );

        var missingCheckConstraints = DiffCheckConstraints(
            expected,
            actual,
            availableColumns,
            BlockedIfIncluded(DmReconcileElements.CheckConstraints),
            supportsCheckConstraints
        );

        var missingDefaultConstraints = DiffDefaultConstraints(
            expected,
            actual,
            availableColumns,
            BlockedIfIncluded(DmReconcileElements.DefaultConstraints)
        );

        var missingIndexes = DiffIndexes(
            expected,
            actual,
            drift,
            availableColumns,
            BlockedIfIncluded(DmReconcileElements.Indexes)
        );

        var missingForeignKeys = DiffForeignKeyConstraints(
            expected,
            actual,
            drift,
            availableColumns,
            BlockedIfIncluded(DmReconcileElements.ForeignKeyConstraints)
        );

        return new DmTableReconciliationPlan
        {
            SchemaName = expected.SchemaName,
            TableName = expected.TableName,
            TableMissing = false,
            Elements = elements,
            MissingColumns = missingColumns,
            MissingPrimaryKeyConstraint = elements.HasFlag(DmReconcileElements.PrimaryKey) ? missingPrimaryKey : null,
            MissingUniqueConstraints = elements.HasFlag(DmReconcileElements.UniqueConstraints)
                ? [.. missingUniqueConstraints.Where(u => MayAdd(DmSchemaElementType.UniqueConstraint, u.ConstraintName))]
                : [],
            MissingCheckConstraints = elements.HasFlag(DmReconcileElements.CheckConstraints)
                ? [.. missingCheckConstraints.Where(c => MayAdd(DmSchemaElementType.CheckConstraint, c.ConstraintName))]
                : [],
            MissingDefaultConstraints = elements.HasFlag(DmReconcileElements.DefaultConstraints)
                ? missingDefaultConstraints
                : [],
            MissingIndexes = elements.HasFlag(DmReconcileElements.Indexes)
                ? [.. missingIndexes.Where(i => MayAdd(DmSchemaElementType.Index, i.IndexName))]
                : [],
            MissingForeignKeyConstraints = elements.HasFlag(DmReconcileElements.ForeignKeyConstraints)
                ? [.. missingForeignKeys.Where(f => MayAdd(DmSchemaElementType.ForeignKeyConstraint, f.ConstraintName))]
                : [],
            Alterations = alterations,
            Renames = renames,
            Drift = drift,
            Blocked = blocked,
        };
    }

    private static (List<DmColumn> Missing, List<DmColumnAlteration> Alterations) DiffColumns(
        DmTable expected,
        DmTable actual,
        List<DmSchemaDrift> drift,
        List<DmSchemaAddition> blocked,
        HashSet<(DmSchemaElementType, string)> doNotAdd,
        bool allowSafeAlterations,
        DbProviderType providerType
    )
    {
        var missing = new List<DmColumn>();
        var alterations = new List<DmColumnAlteration>();

        foreach (var expectedColumn in expected.Columns)
        {
            var actualColumn = actual.Columns.FirstOrDefault(c =>
                c.ColumnName.Equals(expectedColumn.ColumnName, NameComparison)
            );

            if (actualColumn is null)
            {
                missing.Add(expectedColumn);
                continue;
            }

            if (CompareColumn(expectedColumn, actualColumn, drift, allowSafeAlterations, providerType) is not { } alteration)
            {
                continue;
            }

            if (KnownRefusal(actual, alteration, actualColumn, providerType) is { } reason)
            {
                blocked.Add(new DmSchemaAddition(DmSchemaElementType.Column, alteration.ColumnName, reason));
                continue;
            }

            alterations.Add(alteration);
        }

        // A column in the database that the model lacks is reported, never dropped. With exactly one model column
        // missing and one database column unaccounted for, of the same type, the likeliest story is a rename the
        // model did not declare. That is worth saying: adding the new column leaves the data behind in the old one.
        var databaseOnly = actual
            .Columns.Where(a => !expected.Columns.Any(e => e.ColumnName.Equals(a.ColumnName, NameComparison)))
            .ToList();

        foreach (var column in databaseOnly)
        {
            var renamedTo = expected.Columns.FirstOrDefault(e =>
                e.PreviousNames.Any(p => p.Equals(column.ColumnName, NameComparison))
            );

            var description = renamedTo switch
            {
                not null when doNotAdd.Contains((DmSchemaElementType.Column, renamedTo.ColumnName)) =>
                    $"Column '{column.ColumnName}' is one of several previous names of '{renamedTo.ColumnName}' found in the database, so none was renamed.",
                not null when missing.Contains(renamedTo) =>
                    $"Column '{column.ColumnName}' is a previous name of '{renamedTo.ColumnName}' and is renamed when columns are reconciled.",
                not null =>
                    $"Column '{column.ColumnName}' is a previous name of '{renamedTo.ColumnName}', which also exists, so nothing was renamed.",
                null when databaseOnly.Count == 1 && missing.Count == 1 && IsSameKindOfColumn(missing[0], column) =>
                    $"Column '{column.ColumnName}' is not in the model. If it was renamed to '{missing[0].ColumnName}', declare that in PreviousNames, so that it is renamed rather than a new, empty column added beside it.",
                null => $"Column '{column.ColumnName}' is not in the model.",
            };

            drift.Add(new DmSchemaDrift(DmSchemaElementType.Column, column.ColumnName, "absent", "present", description));
        }

        return (missing, alterations);
    }

    private static bool IsSameKindOfColumn(DmColumn model, DmColumn actual) =>
        model.DotnetType is not null
        && actual.DotnetType is not null
        && model.DotnetType.OrUnderlyingTypeIfNullable() == actual.DotnetType.OrUnderlyingTypeIfNullable();

    /// <summary>
    /// Compares a column that exists on both sides and records any drift.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only facets that survive a round trip through every provider are compared. The stored SQL type
    /// name is deliberately not compared: providers rewrite type names and synthesize defaults
    /// (PostgreSQL reports "character varying", MySQL forces datetime precision), which would make the
    /// drift report noisy enough to be ignored, which is worse than reporting less.
    /// </para>
    /// <para>
    /// A facet is only compared when both sides state a positive value. A non-positive length means
    /// "unconstrained" rather than a real bound -- SQLite stores every string as TEXT regardless of the
    /// declared length, and PostgreSQL reports the same for <c>text</c> -- so comparing against it would
    /// flag every string column on those providers.
    /// </para>
    /// </remarks>
    private static DmColumnAlteration? CompareColumn(
        DmColumn expected,
        DmColumn actual,
        List<DmSchemaDrift> drift,
        bool allowSafeAlterations,
        DbProviderType providerType
    )
    {
        // An alteration restates the column's type as the database spells it, changing only the facets that
        // differ, so the base type -- and with it unicode-ness, fixed length, money versus decimal -- can never
        // change by accident. A type the database did not report cannot be restated, so it is only drift.
        var actualType = actual.GetProviderDataType(providerType);
        var canAlter = allowSafeAlterations && !string.IsNullOrWhiteSpace(actualType);

        var changes = new List<string>();
        var isNullable = actual.IsNullable;
        int? newLength = null;
        (int Precision, int Scale)? newNumeric = null;

        // A primary key column is implicitly NOT NULL everywhere, so a nullability difference against
        // a key column says nothing about the model having drifted.
        if (expected.IsNullable != actual.IsNullable && !expected.IsPrimaryKey && !actual.IsPrimaryKey)
        {
            if (canAlter && expected.IsNullable)
            {
                isNullable = true;
                changes.Add("NOT NULL to NULL");
            }
            else
            {
                drift.Add(
                    new DmSchemaDrift(
                        DmSchemaElementType.Column,
                        expected.ColumnName,
                        expected.IsNullable ? "NULL" : "NOT NULL",
                        actual.IsNullable ? "NULL" : "NOT NULL",
                        $"Column '{expected.ColumnName}' nullability differs."
                    )
                );
            }
        }

        // Length/precision/scale are only compared when the model states them and the database
        // reports one, so an unspecified model facet never counts as drift against a provider default.
        if (IsStated(expected.Length) && IsStated(actual.Length) && expected.Length != actual.Length)
        {
            if (canAlter && expected.Length > actual.Length && IsVariableLengthType(actualType!))
            {
                newLength = expected.Length;
                changes.Add($"length {actual.Length} to {expected.Length}");
            }
            else
            {
                drift.Add(
                    new DmSchemaDrift(
                        DmSchemaElementType.Column,
                        expected.ColumnName,
                        $"length {expected.Length}",
                        $"length {actual.Length}",
                        $"Column '{expected.ColumnName}' length differs."
                    )
                );
            }
        }

        var precisionDiffers =
            IsStated(expected.Precision) && IsStated(actual.Precision) && expected.Precision != actual.Precision;
        var scaleDiffers = IsStated(expected.Scale) && IsStated(actual.Scale) && expected.Scale != actual.Scale;

        if (precisionDiffers || scaleDiffers)
        {
            // Precision and scale move together: decimal(10,2) to (10,4) raises the scale but takes two digits
            // from before the point. A change is safe only when neither the scale nor the digits before the
            // point shrink. An unstated scale is zero.
            var oldPrecision = actual.Precision ?? 0;
            var oldScale = Math.Max(actual.Scale ?? 0, 0);
            var precision = precisionDiffers ? expected.Precision!.Value : oldPrecision;
            var scale = scaleDiffers ? expected.Scale!.Value : oldScale;
            var keepsEveryDigit = scale >= oldScale && precision - scale >= oldPrecision - oldScale;

            if (canAlter && keepsEveryDigit && IsExactNumericType(actualType!))
            {
                newNumeric = (precision, scale);
                changes.Add($"precision and scale ({oldPrecision},{oldScale}) to ({precision},{scale})");
            }
            else
            {
                if (precisionDiffers)
                {
                    drift.Add(
                        new DmSchemaDrift(
                            DmSchemaElementType.Column,
                            expected.ColumnName,
                            $"precision {expected.Precision}",
                            $"precision {actual.Precision}",
                            $"Column '{expected.ColumnName}' precision differs."
                        )
                    );
                }

                if (scaleDiffers)
                {
                    drift.Add(
                        new DmSchemaDrift(
                            DmSchemaElementType.Column,
                            expected.ColumnName,
                            $"scale {expected.Scale}",
                            $"scale {actual.Scale}",
                            $"Column '{expected.ColumnName}' scale differs."
                        )
                    );
                }
            }
        }

        if (changes.Count == 0)
        {
            return null;
        }

        var sqlType = actualType!;
        if (newLength is { } length)
        {
            sqlType = WithTypeArguments(sqlType, length.ToString(CultureInfo.InvariantCulture));
        }

        if (newNumeric is { } numeric)
        {
            sqlType = WithTypeArguments(
                sqlType,
                string.Create(CultureInfo.InvariantCulture, $"{numeric.Precision},{numeric.Scale}")
            );
        }

        return new DmColumnAlteration(expected.ColumnName, sqlType, isNullable, changes);
    }

    /// <summary>
    /// Returns the reason a provider is known to refuse an alteration, so it can be blocked in the plan instead
    /// of attempted.
    /// </summary>
    /// <param name="table">The table as it exists in the database.</param>
    /// <param name="alteration">The planned alteration.</param>
    /// <param name="actual">The column as it exists in the database.</param>
    /// <param name="providerType">The database provider.</param>
    /// <returns>The reason, or null when nothing is known to stand in the way.</returns>
    /// <remarks>
    /// Most refusals are caught when the alteration is attempted and reported as blocked then. SQL Server is the
    /// exception: its refusal of ALTER COLUMN ends the caller's transaction, so there is nothing to recover to,
    /// and the cases it is known to refuse have to be recognised beforehand. Probed against SQL Server 2017,
    /// 2019 and 2022: changing a decimal's precision is refused while any index covers the column, and allowing
    /// nulls is accepted on indexed and foreign key columns alike. Resizing a primary or foreign key column is
    /// blocked because SQL Server's documentation lists those columns among the ones ALTER COLUMN refuses; that
    /// case has not been probed.
    /// </remarks>
    private static string? KnownRefusal(
        DmTable table,
        DmColumnAlteration alteration,
        DmColumn actual,
        DbProviderType providerType
    )
    {
        if (providerType != DbProviderType.SqlServer)
        {
            return null;
        }

        var actualType = actual.GetProviderDataType(providerType) ?? string.Empty;
        var changesSize = !alteration.SqlType.Equals(actualType, StringComparison.OrdinalIgnoreCase);
        if (!changesSize)
        {
            return null;
        }

        bool Covers(IEnumerable<DmOrderedColumn> columns) =>
            columns.Any(c => c.ColumnName.Equals(alteration.ColumnName, NameComparison));

        if (
            (table.PrimaryKeyConstraint is { } pk && Covers(pk.Columns))
            || table.ForeignKeyConstraints.Any(fk => Covers(fk.SourceColumns))
        )
        {
            return "SQL Server cannot resize a column in a primary or foreign key";
        }

        if (
            IsExactNumericType(actualType)
            && (table.Indexes.Any(ix => Covers(ix.Columns)) || table.UniqueConstraints.Any(uc => Covers(uc.Columns)))
        )
        {
            return "SQL Server cannot change the precision of a column an index covers";
        }

        return null;
    }

    /// <summary>
    /// Returns whether a column type is a variable-length string or binary type with a stated length, which
    /// can be lengthened without touching the stored values.
    /// </summary>
    /// <remarks>
    /// Fixed-length types are excluded: lengthening <c>char(5)</c> pads every existing value.
    /// </remarks>
    private static bool IsVariableLengthType(string sqlType) =>
        sqlType.Contains('(', StringComparison.Ordinal)
        && sqlType.DiscardLengthPrecisionAndScaleFromSqlTypeName().ToLowerInvariant()
            is "varchar"
                or "nvarchar"
                or "varbinary"
                or "character varying";

    /// <summary>
    /// Returns whether a column type is an exact numeric type with a stated precision.
    /// </summary>
    /// <remarks>
    /// Matched on the first word, so MySQL's <c>decimal(10,2) unsigned</c> qualifies.
    /// </remarks>
    private static bool IsExactNumericType(string sqlType) =>
        sqlType.Contains('(', StringComparison.Ordinal)
        && sqlType.DiscardLengthPrecisionAndScaleFromSqlTypeName().Split(' ')[0].ToLowerInvariant()
            is "decimal"
                or "numeric";

    /// <summary>
    /// Replaces the first parenthesised argument list in a type name, so <c>varchar(50)</c> becomes
    /// <c>varchar(200)</c> and <c>decimal(10,2) unsigned</c> becomes <c>decimal(12,2) unsigned</c>.
    /// </summary>
    private static string WithTypeArguments(string sqlType, string arguments)
    {
        var open = sqlType.IndexOf('(', StringComparison.Ordinal);
        var close = sqlType.IndexOf(')', open + 1);
        return sqlType.Substring(0, open + 1) + arguments + sqlType[close..];
    }

    private static DmPrimaryKeyConstraint? DiffPrimaryKey(
        DmTable expected,
        DmTable actual,
        List<DmSchemaDrift> drift,
        HashSet<string> availableColumns,
        List<DmSchemaAddition> blocked
    )
    {
        if (expected.PrimaryKeyConstraint is null)
        {
            return null;
        }

        if (actual.PrimaryKeyConstraint is null)
        {
            return TryBlock(
                DmSchemaElementType.PrimaryKeyConstraint,
                expected.PrimaryKeyConstraint.ConstraintName,
                expected.PrimaryKeyConstraint.Columns.Select(c => c.ColumnName),
                availableColumns,
                blocked
            )
                ? null
                : expected.PrimaryKeyConstraint;
        }

        // The table already has a primary key. Replacing it is destructive, so the only thing to do is
        // report a difference. Note that MySQL and MariaDB always name the primary key "PRIMARY" and
        // discard any supplied name, so a name-only difference is expected there and is not reported.
        if (
            !SameColumnSet(expected.PrimaryKeyConstraint.Columns, actual.PrimaryKeyConstraint.Columns)
        )
        {
            drift.Add(
                new DmSchemaDrift(
                    DmSchemaElementType.PrimaryKeyConstraint,
                    expected.PrimaryKeyConstraint.ConstraintName,
                    DescribeColumns(expected.PrimaryKeyConstraint.Columns),
                    DescribeColumns(actual.PrimaryKeyConstraint.Columns),
                    "Primary key columns differ."
                )
            );
        }

        return null;
    }

    private static List<DmUniqueConstraint> DiffUniqueConstraints(
        DmTable expected,
        DmTable actual,
        List<DmSchemaDrift> drift,
        HashSet<string> availableColumns,
        List<DmSchemaAddition> blocked
    )
    {
        var missing = new List<DmUniqueConstraint>();

        foreach (var expectedUc in expected.UniqueConstraints)
        {
            var byName = actual.UniqueConstraints.FirstOrDefault(c =>
                c.ConstraintName.Equals(expectedUc.ConstraintName, NameComparison)
            );

            if (byName is not null)
            {
                if (!SameColumnSet(expectedUc.Columns, byName.Columns))
                {
                    drift.Add(
                        new DmSchemaDrift(
                            DmSchemaElementType.UniqueConstraint,
                            expectedUc.ConstraintName,
                            DescribeColumns(expectedUc.Columns),
                            DescribeColumns(byName.Columns),
                            $"Unique constraint '{expectedUc.ConstraintName}' covers different columns."
                        )
                    );
                }

                continue;
            }

            // The constraint may exist under a name the provider chose. Creating a second constraint
            // over the same columns would be redundant, so treat it as satisfied and report the name.
            var byColumns = actual.UniqueConstraints.FirstOrDefault(c => SameColumnSet(expectedUc.Columns, c.Columns));
            if (byColumns is not null)
            {
                drift.Add(
                    new DmSchemaDrift(
                        DmSchemaElementType.UniqueConstraint,
                        expectedUc.ConstraintName,
                        $"constraint named '{expectedUc.ConstraintName}'",
                        $"equivalent constraint named '{byColumns.ConstraintName}'",
                        $"A unique constraint over {DescribeColumns(expectedUc.Columns)} exists under a different name."
                    )
                );
                continue;
            }

            if (TryBlock(
                    DmSchemaElementType.UniqueConstraint,
                    expectedUc.ConstraintName,
                    expectedUc.Columns.Select(c => c.ColumnName),
                    availableColumns,
                    blocked
                ))
            {
                continue;
            }

            missing.Add(expectedUc);
        }

        return missing;
    }

    /// <summary>
    /// Diffs check constraints by name only.
    /// </summary>
    /// <remarks>
    /// Expressions are not compared. Every provider rewrites check expressions when storing them --
    /// adding casts, parentheses and schema qualification -- so a textual comparison against the model
    /// reports drift on constraints that are in fact identical.
    /// </remarks>
    private static List<DmCheckConstraint> DiffCheckConstraints(
        DmTable expected,
        DmTable actual,
        HashSet<string> availableColumns,
        List<DmSchemaAddition> blocked,
        bool supportsCheckConstraints
    )
    {
        var missing = new List<DmCheckConstraint>();

        foreach (var expectedCc in expected.CheckConstraints)
        {
            if (actual.CheckConstraints.Any(c => c.ConstraintName.Equals(expectedCc.ConstraintName, NameComparison)))
            {
                continue;
            }

            if (!supportsCheckConstraints)
            {
                blocked.Add(
                    new DmSchemaAddition(
                        DmSchemaElementType.CheckConstraint,
                        expectedCc.ConstraintName,
                        "this database server does not support check constraints"
                    )
                    {
                        Unsupported = true,
                    }
                );
                continue;
            }

            // Only a column-scoped check can be verified up front; a table-scoped expression may name
            // any column, and parsing SQL to find out is not something this layer should attempt.
            if (
                expectedCc.ColumnName is { Length: > 0 }
                && TryBlock(
                    DmSchemaElementType.CheckConstraint,
                    expectedCc.ConstraintName,
                    [expectedCc.ColumnName],
                    availableColumns,
                    blocked
                )
            )
            {
                continue;
            }

            missing.Add(expectedCc);
        }

        return missing;
    }

    /// <summary>
    /// Diffs default constraints by the column they apply to.
    /// </summary>
    /// <remarks>
    /// A column carries at most one default, so the column -- not the constraint name -- is the identity
    /// here. Expressions are not compared, for the same reason check expressions are not: providers
    /// rewrite them on the way in.
    /// </remarks>
    private static List<DmDefaultConstraint> DiffDefaultConstraints(
        DmTable expected,
        DmTable actual,
        HashSet<string> availableColumns,
        List<DmSchemaAddition> blocked
    )
    {
        var missing = new List<DmDefaultConstraint>();

        foreach (var expectedDc in expected.DefaultConstraints)
        {
            if (actual.DefaultConstraints.Any(c => c.ColumnName.Equals(expectedDc.ColumnName, NameComparison)))
            {
                continue;
            }

            if (TryBlock(
                    DmSchemaElementType.DefaultConstraint,
                    expectedDc.ConstraintName,
                    [expectedDc.ColumnName],
                    availableColumns,
                    blocked
                ))
            {
                continue;
            }

            missing.Add(expectedDc);
        }

        return missing;
    }

    private static List<DmIndex> DiffIndexes(
        DmTable expected,
        DmTable actual,
        List<DmSchemaDrift> drift,
        HashSet<string> availableColumns,
        List<DmSchemaAddition> blocked
    )
    {
        var missing = new List<DmIndex>();

        foreach (var expectedIx in expected.Indexes)
        {
            var byName = actual.Indexes.FirstOrDefault(i => i.IndexName.Equals(expectedIx.IndexName, NameComparison));

            if (byName is not null)
            {
                if (!SameOrderedColumns(expectedIx.Columns, byName.Columns))
                {
                    drift.Add(
                        new DmSchemaDrift(
                            DmSchemaElementType.Index,
                            expectedIx.IndexName,
                            DescribeColumns(expectedIx.Columns),
                            DescribeColumns(byName.Columns),
                            $"Index '{expectedIx.IndexName}' covers different columns."
                        )
                    );
                }
                else if (expectedIx.IsUnique != byName.IsUnique)
                {
                    drift.Add(
                        new DmSchemaDrift(
                            DmSchemaElementType.Index,
                            expectedIx.IndexName,
                            expectedIx.IsUnique ? "UNIQUE" : "non-unique",
                            byName.IsUnique ? "UNIQUE" : "non-unique",
                            $"Index '{expectedIx.IndexName}' uniqueness differs."
                        )
                    );
                }

                continue;
            }

            // Unique constraints are commonly backed by an index the provider names itself, so an
            // index over the same ordered columns already does the job.
            var byColumns = actual.Indexes.FirstOrDefault(i =>
                SameOrderedColumns(expectedIx.Columns, i.Columns) && i.IsUnique == expectedIx.IsUnique
            );

            if (byColumns is not null)
            {
                drift.Add(
                    new DmSchemaDrift(
                        DmSchemaElementType.Index,
                        expectedIx.IndexName,
                        $"index named '{expectedIx.IndexName}'",
                        $"equivalent index named '{byColumns.IndexName}'",
                        $"An index over {DescribeColumns(expectedIx.Columns)} exists under a different name."
                    )
                );
                continue;
            }

            if (TryBlock(
                    DmSchemaElementType.Index,
                    expectedIx.IndexName,
                    expectedIx.Columns.Select(c => c.ColumnName),
                    availableColumns,
                    blocked
                ))
            {
                continue;
            }

            missing.Add(expectedIx);
        }

        return missing;
    }

    private static List<DmForeignKeyConstraint> DiffForeignKeyConstraints(
        DmTable expected,
        DmTable actual,
        List<DmSchemaDrift> drift,
        HashSet<string> availableColumns,
        List<DmSchemaAddition> blocked
    )
    {
        var missing = new List<DmForeignKeyConstraint>();

        foreach (var expectedFk in expected.ForeignKeyConstraints)
        {
            var byName = actual.ForeignKeyConstraints.FirstOrDefault(f =>
                f.ConstraintName.Equals(expectedFk.ConstraintName, NameComparison)
            );

            if (byName is not null)
            {
                if (
                    !SameColumnSet(expectedFk.SourceColumns, byName.SourceColumns)
                    || !expectedFk.ReferencedTableName.Equals(byName.ReferencedTableName, NameComparison)
                    || !SameColumnSet(expectedFk.ReferencedColumns, byName.ReferencedColumns)
                )
                {
                    drift.Add(
                        new DmSchemaDrift(
                            DmSchemaElementType.ForeignKeyConstraint,
                            expectedFk.ConstraintName,
                            $"{DescribeColumns(expectedFk.SourceColumns)} -> {expectedFk.ReferencedTableName}({DescribeColumns(expectedFk.ReferencedColumns)})",
                            $"{DescribeColumns(byName.SourceColumns)} -> {byName.ReferencedTableName}({DescribeColumns(byName.ReferencedColumns)})",
                            $"Foreign key '{expectedFk.ConstraintName}' references differ."
                        )
                    );
                }
                else if (
                    !SameReferentialAction(expectedFk.OnDelete, byName.OnDelete)
                    || !SameReferentialAction(expectedFk.OnUpdate, byName.OnUpdate)
                )
                {
                    drift.Add(
                        new DmSchemaDrift(
                            DmSchemaElementType.ForeignKeyConstraint,
                            expectedFk.ConstraintName,
                            $"ON DELETE {expectedFk.OnDelete}, ON UPDATE {expectedFk.OnUpdate}",
                            $"ON DELETE {byName.OnDelete}, ON UPDATE {byName.OnUpdate}",
                            $"Foreign key '{expectedFk.ConstraintName}' referential actions differ."
                        )
                    );
                }

                continue;
            }

            var byDefinition = actual.ForeignKeyConstraints.FirstOrDefault(f =>
                SameColumnSet(expectedFk.SourceColumns, f.SourceColumns)
                && expectedFk.ReferencedTableName.Equals(f.ReferencedTableName, NameComparison)
                && SameColumnSet(expectedFk.ReferencedColumns, f.ReferencedColumns)
            );

            if (byDefinition is not null)
            {
                drift.Add(
                    new DmSchemaDrift(
                        DmSchemaElementType.ForeignKeyConstraint,
                        expectedFk.ConstraintName,
                        $"constraint named '{expectedFk.ConstraintName}'",
                        $"equivalent constraint named '{byDefinition.ConstraintName}'",
                        "An equivalent foreign key exists under a different name."
                    )
                );
                continue;
            }

            // Only the source columns are checked. The referenced columns live in another table that
            // this single-table primitive does not read, so the database validates those.
            if (TryBlock(
                    DmSchemaElementType.ForeignKeyConstraint,
                    expectedFk.ConstraintName,
                    expectedFk.SourceColumns.Select(c => c.ColumnName),
                    availableColumns,
                    blocked
                ))
            {
                continue;
            }

            missing.Add(expectedFk);
        }

        return missing;
    }

    /// <summary>
    /// Compares two referential actions, treating NO ACTION and RESTRICT as equivalent.
    /// </summary>
    /// <remarks>
    /// The two differ only in when the check runs -- RESTRICT is immediate, NO ACTION is deferrable --
    /// and providers do not agree on which one an unspecified clause means or which one they report
    /// back. MySQL reports RESTRICT for a clause that was never written; PostgreSQL reports NO ACTION.
    /// Distinguishing them here would flag a model as drifted purely for having been read back by a
    /// different provider, so only a genuine change of behaviour is reported.
    /// </remarks>
    private static bool SameReferentialAction(DmForeignKeyAction left, DmForeignKeyAction right)
    {
        return Normalize(left) == Normalize(right);

        static DmForeignKeyAction Normalize(DmForeignKeyAction action) =>
            action == DmForeignKeyAction.Restrict ? DmForeignKeyAction.NoAction : action;
    }

    /// <summary>
    /// Records an element as blocked when any column it depends on will not exist, and reports whether
    /// it did so.
    /// </summary>
    private static bool TryBlock(
        DmSchemaElementType elementType,
        string name,
        IEnumerable<string> requiredColumns,
        HashSet<string> availableColumns,
        List<DmSchemaAddition> blocked
    )
    {
        var absent = requiredColumns.Where(c => !availableColumns.Contains(c)).ToList();
        if (absent.Count == 0)
        {
            return false;
        }

        blocked.Add(
            new DmSchemaAddition(
                elementType,
                name,
                $"requires column(s) {string.Join(", ", absent)}, which are not present and not being added"
            )
        );
        return true;
    }

    /// <summary>
    /// Returns whether a column facet carries a real, comparable value.
    /// </summary>
    /// <remarks>
    /// Zero and negative values both mean "no bound stated". DapperMatic uses -1 for unlimited, and
    /// providers report 0 or -1 for types that carry no length of their own.
    /// </remarks>
    private static bool IsStated(int? facet) => facet is > 0;

    /// <summary>
    /// Compares two column lists ignoring order, which is the right semantics for constraints.
    /// </summary>
    private static bool SameColumnSet(IEnumerable<DmOrderedColumn> left, IEnumerable<DmOrderedColumn> right)
    {
        var l = left.Select(c => c.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var r = right.Select(c => c.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return l.SetEquals(r);
    }

    /// <summary>
    /// Compares two column lists honouring order, which is the right semantics for indexes.
    /// </summary>
    private static bool SameOrderedColumns(IReadOnlyList<DmOrderedColumn> left, IReadOnlyList<DmOrderedColumn> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!left[i].ColumnName.Equals(right[i].ColumnName, NameComparison))
            {
                return false;
            }
        }

        return true;
    }

    private static string DescribeColumns(IEnumerable<DmOrderedColumn> columns)
    {
        return $"({string.Join(", ", columns.Select(c => c.ColumnName))})";
    }

    #endregion // Reconciliation diff
}
