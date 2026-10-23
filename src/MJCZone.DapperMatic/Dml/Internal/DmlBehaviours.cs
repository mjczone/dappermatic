// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Reflection;
using System.Security.Cryptography;
using MJCZone.DapperMatic.DataAnnotations;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// The columns of a model the DML methods fill or filter on their own: soft delete, the concurrency token, audit
/// columns and the tenant key.
/// </summary>
internal sealed class DmlBehaviours
{
    private DmlBehaviours() { }

    /// <summary>Gets the column that says a row is deleted: a bool, or a nullable time.</summary>
    public DmlColumn? SoftDelete { get; private init; }

    /// <summary>Gets the concurrency token.</summary>
    public DmlColumn? Token { get; private init; }

    /// <summary>Gets the time a row was inserted.</summary>
    public DmlColumn? CreatedAt { get; private init; }

    /// <summary>Gets the time a row was last written.</summary>
    public DmlColumn? UpdatedAt { get; private init; }

    /// <summary>Gets the user who inserted a row.</summary>
    public DmlColumn? CreatedBy { get; private init; }

    /// <summary>Gets the user who last wrote a row.</summary>
    public DmlColumn? UpdatedBy { get; private init; }

    /// <summary>Gets the tenant key.</summary>
    public DmlColumn? TenantKey { get; private init; }

    /// <summary>Gets a value indicating whether the model has any of the columns.</summary>
    public bool Any => SoftDelete is not null || Token is not null || CreatedAt is not null || UpdatedAt is not null
        || CreatedBy is not null || UpdatedBy is not null || TenantKey is not null;

    /// <summary>
    /// Gets the columns an update leaves as they are: when a row was inserted, by whom, and its tenant.
    /// </summary>
    public IEnumerable<DmlColumn> Unchanging => new[] { CreatedAt, CreatedBy, TenantKey }.OfType<DmlColumn>();

    /// <summary>
    /// Finds a model's columns, checking each is of a type it can be.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The columns.</returns>
    /// <exception cref="InvalidOperationException">A column is marked twice, or is of a type it cannot be.</exception>
    public static DmlBehaviours Of(DmlModel model)
    {
        DmlColumn? Find<TAttribute>(string what, params Type[] types)
            where TAttribute : Attribute
        {
            var marked = model.Columns.Where(c => c.Property.GetCustomAttribute<TAttribute>(inherit: true) is not null).ToList();
            if (marked.Count > 1)
            {
                throw new InvalidOperationException(
                    $"{model.Type.Name} marks {marked.Count} properties as its {what}: {string.Join(", ", marked.Select(c => c.Property.Name))}."
                );
            }

            var column = marked.FirstOrDefault();
            if (column is not null && types.Length > 0 && !types.Contains(column.Property.PropertyType))
            {
                throw new InvalidOperationException(
                    $"{model.Type.Name}.{column.Property.Name} is its {what}, and is a {column.Property.PropertyType.Name}: it must "
                        + $"be one of {string.Join(", ", types.Select(Name))}."
                );
            }

            return column;
        }

        Type[] times = [typeof(DateTime), typeof(DateTime?), typeof(DateTimeOffset), typeof(DateTimeOffset?)];
        return new DmlBehaviours
        {
            SoftDelete = Find<DmSoftDeleteAttribute>("soft delete", typeof(bool), typeof(bool?), typeof(DateTime?), typeof(DateTimeOffset?)),
            Token = Find<DmConcurrencyTokenAttribute>("concurrency token", typeof(int), typeof(long), typeof(Guid), typeof(byte[])),
            CreatedAt = Find<DmCreatedAtAttribute>("insert time", times),
            UpdatedAt = Find<DmUpdatedAtAttribute>("update time", times),
            CreatedBy = Find<DmCreatedByAttribute>("inserting user"),
            UpdatedBy = Find<DmUpdatedByAttribute>("updating user"),
            TenantKey = Find<DmTenantKeyAttribute>("tenant key"),
        };
    }

    /// <summary>
    /// Tells whether the token is a <c>rowversion</c> SQL Server writes itself.
    /// </summary>
    /// <param name="dialect">The dialect.</param>
    /// <returns>True on SQL Server for a <c>rowversion</c> or <c>timestamp</c> column.</returns>
    public bool DatabaseWritesToken(DmlDialect dialect) =>
        Token is { } token
        && token.Property.PropertyType == typeof(byte[])
        && dialect.ProviderType == DbProviderType.SqlServer
        && (token.Column.GetProviderDataType(DbProviderType.SqlServer) ?? token.Column.GetProviderDataType(DbProviderType.Other)) is { } type
        && (type.StartsWith("rowversion", StringComparison.OrdinalIgnoreCase) || type.StartsWith("timestamp", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Makes the token's next value.
    /// </summary>
    /// <param name="current">Its current value.</param>
    /// <returns>The next one.</returns>
    public object NextToken(object? current) =>
        Token!.Property.PropertyType switch
        {
            var t when t == typeof(int) => (current is int i ? i : 0) + 1,
            var t when t == typeof(long) => (current is long l ? l : 0L) + 1,
            var t when t == typeof(Guid) => Guid.NewGuid(),
            _ => RandomNumberGenerator.GetBytes(8),
        };

    /// <summary>
    /// Makes the value that says a row is deleted.
    /// </summary>
    /// <returns>True, or the current time.</returns>
    public object DeletedValue() => SoftDelete!.Property.PropertyType == typeof(bool) || SoftDelete.Property.PropertyType == typeof(bool?)
        ? true
        : Now(SoftDelete);

    /// <summary>
    /// Reads the clock as a time column's type.
    /// </summary>
    /// <param name="column">The column.</param>
    /// <returns>
    /// The current time in UTC: a <c>DateTime</c> of <see cref="DateTimeKind.Unspecified"/>, which every provider stores
    /// as it is (PostgreSQL's <c>timestamp</c> refuses a UTC one, and System.Data.SQLite reads one back in local time).
    /// </returns>
    public static object Now(DmlColumn column)
    {
        var now = DmDefaults.Clock.GetUtcNow();
        var type = Nullable.GetUnderlyingType(column.Property.PropertyType) ?? column.Property.PropertyType;
        return type == typeof(DateTimeOffset) ? now : (object)DateTime.SpecifyKind(now.UtcDateTime, DateTimeKind.Unspecified);
    }

    private static string Name(Type type) =>
        Nullable.GetUnderlyingType(type) is { } underlying ? underlying.Name + "?" : type.Name;
}
