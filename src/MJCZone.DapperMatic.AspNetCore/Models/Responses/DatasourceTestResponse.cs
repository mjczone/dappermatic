// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for testing datasource connectivity.
/// </summary>
public class DatasourceTestResponse : ResponseBase<DatasourceTestResult>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DatasourceTestResponse"/> class.
    /// </summary>
    public DatasourceTestResponse()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatasourceTestResponse"/> class.
    /// </summary>
    /// <param name="result">The test result.</param>
    public DatasourceTestResponse(DatasourceTestResult result)
        : base(result)
    {
    }
}

/// <summary>
/// Result of a datasource connectivity test.
/// </summary>
public class DatasourceTestResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the connection was successful.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Gets or sets the datasource ID that was tested.
    /// </summary>
    public string? DatasourceId { get; set; }

    /// <summary>
    /// Gets or sets the database provider name.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Gets or sets the database server version if available.
    /// </summary>
    public string? ServerVersion { get; set; }

    /// <summary>
    /// Gets or sets the database name if available.
    /// </summary>
    public string? DatabaseName { get; set; }

    /// <summary>
    /// Gets or sets any error message if the connection failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the response time in milliseconds.
    /// </summary>
    public long ResponseTimeMs { get; set; }
}