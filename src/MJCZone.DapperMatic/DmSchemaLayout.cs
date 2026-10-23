// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

#pragma warning disable SA1402 // The layout and its builder belong together.

namespace MJCZone.DapperMatic;

/// <summary>
/// Which models every schema shares, set once at startup: a deployment with a schema per tenant keeps its shared
/// models (plans, settings, the tenants themselves) in one schema, and every other model in each tenant's.
/// </summary>
/// <remarks>
/// <para>
/// The layout is read by every DML scope and by <c>DmMigrator.MigrateSchemasAsync</c>. A scope's schema, from
/// <c>InSchema</c> or <c>DmDefaults.TenantSchema</c>, never moves a shared model: it is read and written in the shared
/// schema. A shared model is migrated once, into the shared schema; every other model into each schema listed.
/// </para>
/// <para>
/// A deployment with one schema needs no layout, and is unchanged by one: it can start single-tenant, with every
/// model in one schema, and later move tenants into schemas of their own, the first tenant staying in the shared schema
/// beside the shared models.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// DmSchemaLayout.Configure(layout => layout
///     .SharedSchema("public")
///     .Shared&lt;Plan&gt;()
///     .Shared(typeof(Tenant), typeof(Setting)));
/// </code>
/// </example>
public static class DmSchemaLayout
{
    private static volatile Layout _current = new(null, new HashSet<Type>());

    /// <summary>
    /// Gets the schema the shared models are in, or null for each model's own.
    /// </summary>
    public static string? SharedSchemaName => _current.SharedSchema;

    /// <summary>
    /// Gets the shared models.
    /// </summary>
    public static IReadOnlyCollection<Type> SharedModels => _current.Shared;

    /// <summary>
    /// Sets the layout, in place of any earlier one.
    /// </summary>
    /// <param name="configure">Declares the shared schema and the shared models.</param>
    public static void Configure(Action<DmSchemaLayoutBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new DmSchemaLayoutBuilder();
        configure(builder);
        _current = new Layout(builder.Schema, [.. builder.Types]);
    }

    /// <summary>
    /// Tells whether a model is shared.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <returns>True if the layout declares it shared.</returns>
    public static bool IsShared(Type type) => _current.Shared.Contains(type);

    private sealed class Layout(string? sharedSchema, HashSet<Type> shared)
    {
        public string? SharedSchema { get; } = sharedSchema;

        public HashSet<Type> Shared { get; } = shared;
    }
}

/// <summary>
/// Declares a <see cref="DmSchemaLayout"/>.
/// </summary>
public sealed class DmSchemaLayoutBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmSchemaLayoutBuilder"/> class.
    /// </summary>
    internal DmSchemaLayoutBuilder() { }

    /// <summary>Gets the shared schema, or null.</summary>
    internal string? Schema { get; private set; }

    /// <summary>Gets the shared models.</summary>
    internal List<Type> Types { get; } = [];

    /// <summary>
    /// Sets the schema the shared models are in.
    /// </summary>
    /// <param name="schemaName">The schema.</param>
    /// <returns>The builder.</returns>
    public DmSchemaLayoutBuilder SharedSchema(string schemaName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
        Schema = schemaName;
        return this;
    }

    /// <summary>
    /// Declares a model shared.
    /// </summary>
    /// <typeparam name="T">The model.</typeparam>
    /// <returns>The builder.</returns>
    public DmSchemaLayoutBuilder Shared<T>()
        where T : class => Shared(typeof(T));

    /// <summary>
    /// Declares models shared.
    /// </summary>
    /// <param name="types">The models.</param>
    /// <returns>The builder.</returns>
    public DmSchemaLayoutBuilder Shared(params Type[] types) => Shared((IEnumerable<Type>)types);

    /// <summary>
    /// Declares models shared, such as those an assembly scan found.
    /// </summary>
    /// <param name="types">The models.</param>
    /// <returns>The builder.</returns>
    public DmSchemaLayoutBuilder Shared(IEnumerable<Type> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        foreach (var type in types)
        {
            ArgumentNullException.ThrowIfNull(type, nameof(types));
            if (!Types.Contains(type))
            {
                Types.Add(type);
            }
        }

        return this;
    }
}
