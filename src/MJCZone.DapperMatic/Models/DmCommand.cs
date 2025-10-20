// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Represents a SQL command with its associated parameters.
/// </summary>
public class DmCommand
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmCommand"/> class.
    /// </summary>
    public DmCommand() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmCommand"/> class with the specified SQL command text and parameters.
    /// </summary>
    /// <param name="sql">The SQL command text.</param>
    /// <param name="parameters">The parameters for the SQL command.</param>
    public DmCommand(string sql, IDictionary<string, object?>? parameters = null)
    {
        Sql = sql;
        Parameters = parameters;
    }

    /// <summary>
    /// Gets or sets the SQL command text.
    /// </summary>
    public string? Sql { get; set; }

    /// <summary>
    /// Gets or sets the parameters for the SQL command.
    /// </summary>
    public IDictionary<string, object?>? Parameters { get; set; }
}
