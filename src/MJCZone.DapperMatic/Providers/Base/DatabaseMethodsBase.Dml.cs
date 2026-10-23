// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Providers.Base;

/// <summary>
/// Naming for the DML layer.
/// </summary>
public abstract partial class DatabaseMethodsBase
{
    /// <summary>
    /// Quotes a column name the way DDL names it: normalized, then in the provider's quote characters, so DML
    /// refers to exactly the column DDL created.
    /// </summary>
    /// <param name="name">The name, as the model gives it.</param>
    /// <returns>The normalized, quoted name.</returns>
    internal string GetNormalizedQuotedIdentifier(string name) => QuoteName(name);

    /// <summary>
    /// Gets a name the way DDL names it, normalized but not quoted, for the APIs that take a column by name: bulk
    /// copy mappings, catalog lookups.
    /// </summary>
    /// <param name="name">The name, as the model gives it.</param>
    /// <returns>The normalized name.</returns>
    internal string GetNormalizedName(string name) => NormalizeName(name);
}
