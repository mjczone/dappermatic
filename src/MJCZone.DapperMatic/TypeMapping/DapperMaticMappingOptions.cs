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
