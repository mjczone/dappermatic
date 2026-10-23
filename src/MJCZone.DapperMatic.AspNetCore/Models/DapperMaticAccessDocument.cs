// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Models;

/// <summary>
/// Policies and tenants, as they are kept, exported and imported.
/// </summary>
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class DapperMaticAccessDocument
{
    /// <summary>Gets the policies.</summary>
    public List<DmPolicy> Policies { get; } = [];

    /// <summary>Gets the tenants, by id.</summary>
    public Dictionary<string, DapperMaticTenantSettings> Tenants { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the webhooks.</summary>
    public List<DapperMaticWebhook> Webhooks { get; } = [];

    /// <summary>Gets the service accounts (their keys are kept apart, and never exported).</summary>
    public List<DapperMaticServiceAccount> ServiceAccounts { get; } = [];

    /// <summary>Gets the JSON options policies and tenants are kept with.</summary>
    internal static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>
    /// Copies the document, so that what is kept is never shared with a caller.
    /// </summary>
    /// <returns>The copy.</returns>
    internal DapperMaticAccessDocument Copy() => JsonSerializer.Deserialize<DapperMaticAccessDocument>(JsonSerializer.Serialize(this, Json), Json)!;
}
