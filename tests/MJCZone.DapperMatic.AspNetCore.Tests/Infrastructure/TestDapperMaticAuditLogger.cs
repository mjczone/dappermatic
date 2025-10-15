// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;

using MJCZone.DapperMatic.AspNetCore.Auditing;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Infrastructure;

/// <summary>
/// Test implementation of DapperMatic audit logger that captures events for verification.
/// </summary>
public class TestDapperMaticAuditLogger : IDapperMaticAuditLogger
{
    private readonly List<DapperMaticAuditEvent> _auditEvents = [];
    private readonly object _lockObject = new();

    /// <summary>
    /// Gets all captured audit events.
    /// </summary>
    public IReadOnlyList<DapperMaticAuditEvent> AuditEvents
    {
        get
        {
            lock (_lockObject)
            {
                return _auditEvents.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets audit events for a specific operation.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <returns>List of audit events for the operation.</returns>
    public IReadOnlyList<DapperMaticAuditEvent> GetEventsForOperation(string operation)
    {
        lock (_lockObject)
        {
            return _auditEvents.Where(e => e.Operation == operation).ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Gets the most recent audit event.
    /// </summary>
    /// <returns>The most recent audit event or null if none exist.</returns>
    public DapperMaticAuditEvent? GetLastEvent()
    {
        lock (_lockObject)
        {
            return _auditEvents.LastOrDefault();
        }
    }

    /// <summary>
    /// Gets the most recent audit event for a specific operation.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <returns>The most recent audit event for the operation or null if none exist.</returns>
    public DapperMaticAuditEvent? GetLastEventForOperation(string operation)
    {
        lock (_lockObject)
        {
            return _auditEvents.Where(e => e.Operation == operation).LastOrDefault();
        }
    }

    /// <summary>
    /// Gets successful audit events.
    /// </summary>
    /// <returns>List of successful audit events.</returns>
    public IReadOnlyList<DapperMaticAuditEvent> GetSuccessfulEvents()
    {
        lock (_lockObject)
        {
            return _auditEvents.Where(e => e.Success).ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Gets failed audit events.
    /// </summary>
    /// <returns>List of failed audit events.</returns>
    public IReadOnlyList<DapperMaticAuditEvent> GetFailedEvents()
    {
        lock (_lockObject)
        {
            return _auditEvents.Where(e => !e.Success).ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Clears all captured audit events.
    /// </summary>
    public void ClearEvents()
    {
        lock (_lockObject)
        {
            _auditEvents.Clear();
        }
    }

    /// <inheritdoc />
    public Task LogOperationAsync(DapperMaticAuditEvent auditEvent)
    {
        lock (_lockObject)
        {
            _auditEvents.Add(auditEvent);
        }

        return Task.CompletedTask;
    }
}
