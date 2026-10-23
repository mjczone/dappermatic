// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.TypeMapping;

/// <summary>
/// Configuration options for DapperMatic type mapping and query compatibility.
/// Controls how type handlers are registered for future DML type mapping features.
/// </summary>
public class DapperMaticMappingOptions
{
    /// <summary>
    /// Gets or sets how to handle type handler registration when handler already exists.
    /// Default: SkipIfExists (don't override user's custom handlers)
    /// </summary>
    public TypeHandlerPrecedence HandlerPrecedence { get; set; } = TypeHandlerPrecedence.SkipIfExists;

    /// <summary>
    /// Gets or sets a value indicating whether to support modern C# records with parameterized constructors.
    /// Default: true (enables record support)
    /// </summary>
    public bool EnableRecordSupport { get; set; } = true;

    /// <summary>
    /// Gets or sets the options used for values stored as JSON: collections, dictionaries, JSON document nodes and
    /// objects of the application's own types. Default: <see cref="System.Text.Json.JsonSerializer"/>'s defaults.
    /// </summary>
    /// <remarks>
    /// Keep the default encoder where JSON columns are not Unicode, as on SQL Server, where DDL creates them as
    /// <c>varchar(max)</c>: it escapes every non-ASCII character, which an encoder such as
    /// <c>UnsafeRelaxedJsonEscaping</c> would write as it is and the column could lose.
    /// </remarks>
    public System.Text.Json.JsonSerializerOptions? JsonSerializerOptions { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether collections of simple values, such as <c>string[]</c>,
    /// <c>List&lt;int&gt;</c> or <c>HashSet&lt;Guid&gt;</c>, are stored as JSON. Default: false.
    /// </summary>
    /// <remarks>
    /// Dapper uses one handler per type for every parameter, and a handler stops it expanding a collection for
    /// <c>WHERE id IN @ids</c>. So by default these collections keep Dapper's own behaviour: they expand for
    /// <c>IN</c>, and PostgreSQL stores arrays natively and accepts them for <c>= ANY(@ids)</c>. Turn this on to
    /// store them as JSON columns on every provider, at the cost of <c>IN</c> lists of those types; or register
    /// only the types needed with <see cref="DapperMaticTypeMapping.RegisterJsonTypes"/>. Collections of objects,
    /// and dictionaries, are always stored as JSON.
    /// </remarks>
    public bool StoreSimpleCollectionsAsJson { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to look through the entry assembly, and the application assemblies
    /// it references, for model property types that need a handler, such as <c>List&lt;OrderLine&gt;</c>.
    /// Default: true.
    /// </summary>
    /// <remarks>
    /// Dapper picks a parameter's handler from the property's declared type and never asks before writing, so a
    /// handler for a type of the application's own must exist before the first insert. Models are also registered
    /// when <see cref="Models.DmTableFactory"/> builds them and the first time Dapper reads one; types it cannot
    /// find can be registered with <see cref="DapperMaticTypeMapping.RegisterModels"/>.
    /// </remarks>
    public bool ScanEntryAssembly { get; set; } = true;
}

/// <summary>
/// Defines how type handler registration should handle conflicts with existing handlers.
/// </summary>
public enum TypeHandlerPrecedence
{
    /// <summary>
    /// Skip registration if handler already exists (don't override user's handlers).
    /// This is the safest option that respects user customizations.
    /// </summary>
    SkipIfExists,

    /// <summary>
    /// Override existing handlers (DapperMatic handlers take precedence).
    /// Use this if you want DapperMatic handlers to always be used.
    /// </summary>
    OverrideExisting,

    /// <summary>
    /// Throw exception if handler already exists (fail fast on conflicts).
    /// Use this during development to detect handler conflicts early.
    /// </summary>
    ThrowIfExists,
}
