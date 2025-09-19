// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore;

/// <summary>
/// Options for configuring DapperMatic ASP.NET Core integration.
/// </summary>
/// <code>
/// // Illustrate appsettings.json configuration
/// {
///   "DapperMatic": {
///     "BasePath": "/api/dm",
///     "RequireAuthentication": false,
///     "Datasources": [
///       {
///         "Id": "mySqlDatasource",
///         "Provider": "SqlServer",
///         "ConnectionString": "Server=myServer;Database=myDb;User Id=myUser;Password=myPass;",
///         "DisplayName": "My Datasource",
///         "Description": "A description of my datasource",
///         "Tags": [ "tag1", "tag2" ],
///         "IsEnabled": true
///       }
///     ],
///     "ConnectionStringEncryptionKey": "base64-encoded-key"
///   }
/// }
/// </code>
public sealed class DapperMaticOptions
{
    /// <summary>
    /// Gets or sets the base path for DapperMatic API endpoints.
    /// Default is "/api/dm".
    /// </summary>
    public string BasePath { get; set; } = "/api/dm";

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether to require authentication for DapperMatic endpoints.
    /// Default is false (no authentication required).
    /// </summary>
    public bool RequireAuthentication { get; set; }

    /// <summary>
    /// Gets or sets the required role for DapperMatic operations.
    /// </summary>
    public string? RequireRole { get; set; }

    /// <summary>
    /// Gets or sets the required role for read-only operations (e.g., "get" operations).
    /// If not set, read-only operations will use the same role as other operations.
    /// Users with the main role can perform all operations regardless of this setting.
    /// </summary>
    public string? ReadOnlyRole { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether to enable CORS for DapperMatic endpoints.
    /// Default is false.
    /// </summary>
    public bool EnableCors { get; set; }

    /// <summary>
    /// Gets or sets the CORS policy name to use for DapperMatic endpoints.
    /// Only used if EnableCors is true.
    /// </summary>
    public string? CorsPolicyName { get; set; }

    /// <summary>
    /// Gets the list of datasources to register during startup.
    /// </summary>
    public List<DatasourceDto> Datasources { get; } = [];

    /// <summary>
    /// Gets or sets the encryption key used to encrypt connection strings in persistent storage.
    /// If not provided, connection strings will be stored in plain text (not recommended for production).
    /// Should be a base64-encoded 256-bit key for AES encryption.
    /// </summary>
    public string? ConnectionStringEncryptionKey { get; set; }
}
