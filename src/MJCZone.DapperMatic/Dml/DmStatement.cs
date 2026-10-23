// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// The SQL a query runs and the values of its parameters, for logging and tests.
/// </summary>
public sealed class DmStatement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmStatement"/> class.
    /// </summary>
    /// <param name="sql">The SQL.</param>
    /// <param name="parameters">The parameters.</param>
    internal DmStatement(string sql, IReadOnlyList<KeyValuePair<string, object?>> parameters)
    {
        Sql = sql;
        Parameters = parameters;
    }

    /// <summary>
    /// Gets the SQL.
    /// </summary>
    public string Sql { get; }

    /// <summary>
    /// Gets the parameters' names, without a prefix, and values.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, object?>> Parameters { get; }

    /// <inheritdoc />
    public override string ToString() => Sql;
}
