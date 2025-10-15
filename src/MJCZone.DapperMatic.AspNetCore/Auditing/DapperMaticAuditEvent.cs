// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Auditing;

/// <summary>
/// Represents an audit event for a DapperMatic operation.
/// </summary>
public class DapperMaticAuditEvent
{
    /// <summary>
    /// Gets or sets the identifier of the user who performed the operation.
    /// </summary>
    public string UserIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the operation that was performed (e.g., "datasources/create", "tables/update").
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the datasource involved in the operation, if applicable.
    /// </summary>
    public string? DatasourceId { get; set; }

    /// <summary>
    /// Gets or sets the name of the schema involved in the operation, if applicable.
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Gets or sets the name of the table being accessed, if applicable.
    /// </summary>
    public string? TableName { get; set; }

    /// <summary>
    /// Gets or sets the name of the view being accessed, if applicable.
    /// </summary>
    public string? ViewName { get; set; }

    /// <summary>
    /// Gets or sets the name of the columns being accessed, if applicable.
    /// </summary>
    public List<string>? ColumnNames { get; set; }

    /// <summary>
    /// Gets or sets the name of the index being accessed, if applicable.
    /// </summary>
    public string? IndexName { get; set; }

    /// <summary>
    /// Gets or sets the name of the constraint being accessed, if applicable.
    /// </summary>
    public string? ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the message describing the operation outcome.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the operation occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the request ID for correlating logs.
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// Gets or sets the IP address of the client.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets additional properties for custom audit information.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = [];
}
