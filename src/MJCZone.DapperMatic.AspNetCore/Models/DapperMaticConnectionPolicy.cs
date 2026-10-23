// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models;

/// <summary>
/// What a datasource added or changed through the API may connect to. A connection string is code that runs on the
/// server: it can name any host, ask the driver to read local files, or point SQLite at any path. Datasources from
/// code or configuration are trusted and not checked.
/// </summary>
public sealed class DapperMaticConnectionPolicy
{
    /// <summary>
    /// Gets the providers allowed, by name (SqlServer, PostgreSql, MySql, Sqlite). Empty allows every provider
    /// with a registered driver.
    /// </summary>
    public List<string> AllowedProviders { get; } = [];

    /// <summary>
    /// Gets the hosts allowed, as names or patterns with <c>*</c> (<c>*.internal.example.com</c>). Empty allows any
    /// host.
    /// </summary>
    public List<string> AllowedHosts { get; } = [];

    /// <summary>
    /// Gets or sets the folder SQLite database files must be in. Null allows only in-memory SQLite databases.
    /// </summary>
    public string? SqliteDirectory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a MySQL connection may allow <c>LOAD DATA LOCAL</c>, which lets the
    /// server ask for files on this machine. Default is false.
    /// </summary>
    public bool AllowLoadLocalInfile { get; set; }
}
