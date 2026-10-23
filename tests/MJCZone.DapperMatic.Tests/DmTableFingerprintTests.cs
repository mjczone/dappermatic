// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Globalization;
using System.Reflection;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// The fingerprint decides whether a table is reconciled at all. A model change it misses is skipped silently,
/// so these tests are about what it must notice; a change it notices needlessly only costs one reconcile.
/// </summary>
public class DmTableFingerprintTests
{
    /// <summary>
    /// Properties deliberately left out of the fingerprint, and why. A property missing from both this list and
    /// the fingerprint fails <see cref="Every_facet_of_the_model_changes_the_fingerprint"/>.
    /// </summary>
    private static readonly (Func<Type, PropertyInfo, bool> Applies, string Reason)[] NotFacets =
    [
        ((_, p) => p.Name == "ConstraintType", "derived from the constraint's class; each kind has its own line"),
        (
            (t, p) => t != typeof(DmTable) && p.Name is "SchemaName" or "TableName",
            "a child repeats its table's schema and name, and creating the table overwrites them with normalized ones"
        ),
    ];

    /// <summary>
    /// Mutates every public property of every model type in turn -- every column facet, every constraint and
    /// index property, every ordered column -- and requires each mutation to change the fingerprint. A property
    /// added to a model later fails here until it is either fingerprinted or listed in <see cref="NotFacets"/>.
    /// </summary>
    [Fact]
    public void Every_facet_of_the_model_changes_the_fingerprint()
    {
        var mutations = new List<(string Path, Action<DmTable> Mutate)>();
        var unknown = new List<string>();
        CollectMutations(typeof(DmTable), t => t, "table", mutations, unknown);

        Assert.True(unknown.Count == 0, "No mutation for: " + string.Join(", ", unknown));

        // Sanity check that the walk reached the nested models.
        Assert.Contains(mutations, m => m.Path == "table.Columns[0].ProviderDataTypes[+]");
        Assert.Contains(mutations, m => m.Path == "table.ForeignKeyConstraints[0].ReferencedColumns[0].Order");
        Assert.Contains(mutations, m => m.Path == "table.PrimaryKeyConstraint.PreviousNames");

        var baseline = DmTableFingerprint.Compute(Model());
        var missed = new List<string>();
        foreach (var (path, mutate) in mutations)
        {
            var table = Model();
            mutate(table);
            if (DmTableFingerprint.Compute(table) == baseline)
            {
                missed.Add(path);
            }
        }

        Assert.True(missed.Count == 0, "The fingerprint did not change for: " + string.Join(", ", missed));
    }

    [Fact]
    public void Equal_models_have_equal_fingerprints()
    {
        Assert.Equal(DmTableFingerprint.Compute(Model()), DmTableFingerprint.Compute(Model()));

        var fromFactory = DmTableFactory.GetTable(typeof(FingerprintModel));
        Assert.Equal(DmTableFingerprint.Compute(fromFactory), DmTableFingerprint.Compute(fromFactory));
    }

    /// <summary>
    /// The order of constraints, indexes and provider data types means nothing, so it must not change the
    /// fingerprint: provider data types live in a plain dictionary, and attribute order is not guaranteed to
    /// survive a refactoring of the model class.
    /// </summary>
    [Fact]
    public void Order_of_unordered_elements_does_not_change_the_fingerprint()
    {
        var reordered = Model();
        reordered.UniqueConstraints.Reverse();
        reordered.CheckConstraints.Reverse();
        reordered.DefaultConstraints.Reverse();
        reordered.Indexes.Reverse();
        reordered.ForeignKeyConstraints.Reverse();
        foreach (var column in reordered.Columns)
        {
            var types = column.ProviderDataTypes.Reverse().ToList();
            column.ProviderDataTypes.Clear();
            foreach (var (provider, type) in types)
            {
                column.ProviderDataTypes[provider] = type;
            }
        }

        Assert.Equal(DmTableFingerprint.Compute(Model()), DmTableFingerprint.Compute(reordered));
    }

    /// <summary>
    /// Column order is the order a missing table is created in, and column order inside an index or key is
    /// part of what it is.
    /// </summary>
    [Fact]
    public void Order_of_columns_changes_the_fingerprint()
    {
        var baseline = DmTableFingerprint.Compute(Model());

        var columns = Model();
        columns.Columns.Reverse();
        Assert.NotEqual(baseline, DmTableFingerprint.Compute(columns));

        var indexColumns = Model();
        indexColumns.Indexes.Single(ix => ix.Columns.Count > 1).Columns.Reverse();
        Assert.NotEqual(baseline, DmTableFingerprint.Compute(indexColumns));
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    [InlineData("de-DE")]
    [InlineData("ja-JP")]
    public void Current_culture_does_not_change_the_fingerprint(string culture)
    {
        var invariant = WithCulture(CultureInfo.InvariantCulture, () => DmTableFingerprint.Compute(Model()));
        var other = WithCulture(new CultureInfo(culture), () => DmTableFingerprint.Compute(Model()));
        Assert.Equal(invariant, other);
    }

    /// <summary>
    /// <see cref="Type.FullName"/> of a generic type embeds the runtime's assembly version, so a fingerprint
    /// built on it would change on every runtime upgrade and reconcile every nullable column's table again.
    /// </summary>
    [Fact]
    public void Fingerprint_does_not_depend_on_assembly_versions()
    {
        var canonical = DmTableFingerprint.GetCanonicalForm(DmTableFactory.GetTable(typeof(FingerprintModel)));

        Assert.Contains("System.Nullable`1[System.Int32]", canonical, StringComparison.Ordinal);
        Assert.DoesNotContain("Version=", canonical, StringComparison.Ordinal);
        Assert.DoesNotContain("PublicKeyToken", canonical, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every value carries its length, so values that would read the same run together stay distinct.
    /// </summary>
    [Fact]
    public void Values_cannot_run_into_each_other()
    {
        var one = Model();
        one.PreviousNames = ["a,b"];
        var two = Model();
        two.PreviousNames = ["a", "b"];
        Assert.NotEqual(DmTableFingerprint.Compute(one), DmTableFingerprint.Compute(two));

        // A value that looks like the rest of a line.
        var plain = Model();
        plain.SchemaName = "app";
        var spoofed = Model();
        spoofed.SchemaName = "app name=6:orders";
        spoofed.TableName = "orders";
        Assert.NotEqual(DmTableFingerprint.Compute(plain), DmTableFingerprint.Compute(spoofed));

        // A missing value is not an empty one.
        var missing = Model();
        missing.Columns[0].ReferencedTableName = null;
        var empty = Model();
        empty.Columns[0].ReferencedTableName = string.Empty;
        Assert.NotEqual(DmTableFingerprint.Compute(missing), DmTableFingerprint.Compute(empty));
    }

    /// <summary>
    /// A stored fingerprint is compared with one computed by a later process, possibly a later version of the
    /// library. If this test fails, the canonical form changed: bump <see cref="DmTableFingerprint.FormatVersion"/>
    /// in the same change and update the expected value, so the change is deliberate.
    /// </summary>
    [Fact]
    public void Fingerprint_is_stable_across_processes_and_releases()
    {
        const string expected = "db192c181cbb79378658fdd0e8953779a67980ea731cefa6e6067c69bcd68946";
        var actual = DmTableFingerprint.Compute(Model());

        Assert.Equal(1, DmTableFingerprint.FormatVersion);
        Assert.True(
            actual == expected,
            $"The fingerprint of an unchanged model is now {actual}. If the canonical form changed on purpose, bump "
                + "DmTableFingerprint.FormatVersion and update this test."
        );
    }

    private static T WithCulture<T>(CultureInfo culture, Func<T> action)
    {
        var (current, currentUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = current;
            CultureInfo.CurrentUICulture = currentUi;
        }
    }

    /// <summary>
    /// A table with every kind of element and every facet set, so that each mutation changes a real value.
    /// </summary>
    private static DmTable Model()
    {
        var code = new DmColumn("app", "orders", "code", typeof(string))
        {
            Length = 20,
            Precision = 10,
            Scale = 2,
            IsNullable = true,
            IsUnique = true,
            IsUnicode = true,
            IsFixedLength = true,
            IsIndexed = true,
            IsForeignKey = true,
            ReferencedTableName = "codes",
            ReferencedColumnName = "code",
            OnDelete = DmForeignKeyAction.Cascade,
            OnUpdate = DmForeignKeyAction.Restrict,
            CheckExpression = "code <> ''",
            DefaultExpression = "'new'",
            PreviousNames = ["order_code"],
        };
        code.ProviderDataTypes[DbProviderType.PostgreSql] = "varchar(20)";
        code.ProviderDataTypes[DbProviderType.SqlServer] = "nvarchar(20)";

        var id = new DmColumn("app", "orders", "id", typeof(int?))
        {
            IsPrimaryKey = true,
            IsAutoIncrement = true,
            DefaultExpressionFunc = _ => "0",
        };
        id.ProviderDataTypes[DbProviderType.Sqlite] = "integer";

        return new DmTable
        {
            SchemaName = "app",
            TableName = "orders",
            PreviousNames = ["order", "purchase"],
            Columns =
            [
                id,
                code,
                new DmColumn("app", "orders", "total", typeof(decimal)) { Precision = 12, Scale = 2 },
            ],
            PrimaryKeyConstraint = new DmPrimaryKeyConstraint("app", "orders", "pk_orders", [new DmOrderedColumn("id")])
            {
                PreviousNames = ["pk_order"],
            },
            UniqueConstraints =
            [
                new DmUniqueConstraint("app", "orders", "uc_orders_code", [new DmOrderedColumn("code")])
                {
                    PreviousNames = ["uc_order_code"],
                },
                new DmUniqueConstraint("app", "orders", "uc_orders_total", [new DmOrderedColumn("total")]),
            ],
            CheckConstraints =
            [
                new DmCheckConstraint("app", "orders", "code", "ck_orders_code", "code <> ''")
                {
                    PreviousNames = ["ck_order_code"],
                },
                new DmCheckConstraint("app", "orders", null, "ck_orders_total", "total >= 0"),
            ],
            DefaultConstraints =
            [
                new DmDefaultConstraint("app", "orders", "code", "df_orders_code", "'new'")
                {
                    PreviousNames = ["df_order_code"],
                },
                new DmDefaultConstraint("app", "orders", "total", "df_orders_total", "0"),
            ],
            Indexes =
            [
                new DmIndex(
                    "app",
                    "orders",
                    "ix_orders_code_total",
                    [new DmOrderedColumn("code"), new DmOrderedColumn("total", DmColumnOrder.Descending)],
                    isUnique: true
                )
                {
                    PreviousNames = ["ix_order_code_total"],
                },
                new DmIndex("app", "orders", "ix_orders_total", [new DmOrderedColumn("total")]),
            ],
            ForeignKeyConstraints =
            [
                new DmForeignKeyConstraint(
                    "app",
                    "orders",
                    "fk_orders_code",
                    [new DmOrderedColumn("code")],
                    "codes",
                    [new DmOrderedColumn("code")],
                    DmForeignKeyAction.Cascade,
                    DmForeignKeyAction.SetNull
                )
                {
                    PreviousNames = ["fk_order_code"],
                },
                new DmForeignKeyConstraint(
                    "app",
                    "orders",
                    "fk_orders_total",
                    [new DmOrderedColumn("total")],
                    "totals",
                    [new DmOrderedColumn("amount")]
                ),
            ],
        };
    }

    /// <summary>
    /// Builds one mutation per property, recursing into nested models through the first element of each list.
    /// Every mutation changes the value it touches, so a mutation that leaves the fingerprint alone has found a
    /// facet the fingerprint does not cover.
    /// </summary>
    private static void CollectMutations(
        Type type,
        Func<DmTable, object> locate,
        string path,
        List<(string Path, Action<DmTable> Mutate)> into,
        List<string> unknown
    )
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (NotFacets.Any(n => n.Applies(type, property)))
            {
                continue;
            }

            var name = $"{path}.{property.Name}";
            var propertyType = property.PropertyType;
            var enumType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            void Set(Func<object?, object?> change) =>
                into.Add(
                    (
                        name,
                        table =>
                        {
                            var owner = locate(table);
                            property.SetValue(owner, change(property.GetValue(owner)));
                        }
                    )
                );

            if (propertyType == typeof(string))
            {
                Set(v => v is null ? "mutated" : v + "_mutated");
            }
            else if (propertyType == typeof(int?))
            {
                Set(v => v is null ? 7 : (int)v + 1);
            }
            else if (propertyType == typeof(bool))
            {
                Set(v => !(bool)v!);
            }
            else if (enumType.IsEnum)
            {
                var values = Enum.GetValues(enumType);
                Set(v =>
                    v is null
                        ? values.GetValue(0)
                        : values.GetValue((Array.IndexOf(values, v) + 1) % values.Length)
                );
            }
            else if (propertyType == typeof(Type))
            {
                Set(v => (Type?)v == typeof(Guid) ? typeof(byte) : typeof(Guid));
            }
            else if (propertyType == typeof(Func<DbProviderType, string>))
            {
                Set(v => v is null ? new Func<DbProviderType, string>(_ => "mutated") : null);
            }
            else if (propertyType == typeof(List<string>))
            {
                into.Add((name, table => ((List<string>)property.GetValue(locate(table))!).Add("mutated")));
            }
            else if (propertyType == typeof(Dictionary<DbProviderType, string>))
            {
                into.Add(
                    (
                        $"{name}[+]",
                        table =>
                            ((Dictionary<DbProviderType, string>)property.GetValue(locate(table))!)[
                                DbProviderType.MySql
                            ] = "mutated"
                    )
                );
                into.Add(
                    (
                        $"{name}[changed]",
                        table =>
                        {
                            var types = (Dictionary<DbProviderType, string>)property.GetValue(locate(table))!;
                            foreach (var provider in types.Keys.ToList())
                            {
                                types[provider] += "_mutated";
                            }
                        }
                    )
                );
            }
            else if (
                propertyType.IsGenericType
                && propertyType.GetGenericTypeDefinition() == typeof(List<>)
                && propertyType.GetGenericArguments()[0].Namespace == typeof(DmTable).Namespace
            )
            {
                var elementType = propertyType.GetGenericArguments()[0];
                IList List(DmTable table) => (IList)property.GetValue(locate(table))!;

                into.Add(($"{name}[-]", table => List(table).RemoveAt(0)));
                CollectMutations(elementType, table => List(table)[0]!, $"{name}[0]", into, unknown);
            }
            else if (propertyType.Namespace == typeof(DmTable).Namespace && propertyType.IsClass)
            {
                Set(_ => null);
                CollectMutations(propertyType, table => property.GetValue(locate(table))!, name, into, unknown);
            }
            else
            {
                unknown.Add($"{name} ({propertyType})");
            }
        }
    }
}

[DmTable("app", "fingerprint_model")]
public class FingerprintModel
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("quantity")]
    public int? Quantity { get; set; }

    [DmColumn("name", length: 50)]
    [DmIndex(false, null, "ix_fingerprint_model_name")]
    public string Name { get; set; } = default!;
}
