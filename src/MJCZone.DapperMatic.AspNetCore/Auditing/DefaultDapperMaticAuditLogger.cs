// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace MJCZone.DapperMatic.AspNetCore.Auditing;

/// <summary>
/// Default implementation of IDapperMaticAuditLogger that uses ILogger.
/// </summary>
public class DefaultDapperMaticAuditLogger : IDapperMaticAuditLogger
{
    private readonly ILogger<DefaultDapperMaticAuditLogger> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDapperMaticAuditLogger"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public DefaultDapperMaticAuditLogger(ILogger<DefaultDapperMaticAuditLogger> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task LogOperationAsync(DapperMaticAuditEvent auditEvent)
    {
        if (auditEvent.Success)
        {
            _logger.LogInformation(
                "DapperMatic Operation: {Operation} by {User} on {Datasource} - Success",
                auditEvent.Operation,
                auditEvent.UserIdentifier,
                auditEvent.DatasourceId ?? "N/A"
            );
        }
        else
        {
            _logger.LogWarning(
                "DapperMatic Operation: {Operation} by {User} on {Datasource} - Failed: {Error}",
                auditEvent.Operation,
                auditEvent.UserIdentifier,
                auditEvent.DatasourceId ?? "N/A",
                auditEvent.Message ?? "Unknown error"
            );
        }

        return Task.CompletedTask;
    }
}
