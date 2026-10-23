// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

#pragma warning disable SA1402 // A webhook and its settings belong together.

using System.Text.Json.Serialization;

namespace MJCZone.DapperMatic.AspNetCore.Models;

/// <summary>What a webhook hears.</summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter<DapperMaticWebhookActions>))]
public enum DapperMaticWebhookActions
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary>Rows inserted.</summary>
    Insert = 1,

    /// <summary>Rows updated.</summary>
    Update = 2,

    /// <summary>Rows deleted.</summary>
    Delete = 4,

    /// <summary>Writes by a function that declares the table in its <c>Writes</c>: which rows is not known.</summary>
    Write = 8,

    /// <summary>Everything.</summary>
    All = Insert | Update | Delete | Write,
}

/// <summary>
/// A web address told of the changes made through DapperMatic to the tables it names: each change is noted in the
/// datasource, in the write's transaction, and sent from there in order, retried until the address takes it. A message
/// names the table, the action and the keys of the rows, never other columns; it is signed with the secret
/// (<c>X-DapperMatic-Signature: sha256=…</c>, an HMAC of <c>{timestamp}.{body}</c>).
/// </summary>
/// <example>
/// <code>
/// options.Webhooks.Add(new DapperMaticWebhook { Name = "orders", Url = "https://hooks.example.com/orders", Secret = "…", Tables = ["orders"] });
/// </code>
/// </example>
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class DapperMaticWebhook
{
    /// <summary>Gets or sets the name, unique within its tenant.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the address messages are posted to: <c>https://</c>, or <c>http://</c>.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the secret messages are signed with, or null to send them unsigned.</summary>
    public string? Secret { get; set; }

    /// <summary>Gets or sets the datasource it hears, or null for every one.</summary>
    public string? DatasourceId { get; set; }

    /// <summary>Gets the schemas it hears, by name or pattern; empty for all.</summary>
    public List<string> Schemas { get; } = [];

    /// <summary>Gets the tables it hears, by name or pattern; empty for all.</summary>
    public List<string> Tables { get; } = [];

    /// <summary>Gets or sets what it hears.</summary>
    public DapperMaticWebhookActions Actions { get; set; } = DapperMaticWebhookActions.All;

    /// <summary>
    /// Gets or sets the tenant whose changes it hears, as one a tenant's administrator keeps: rows marked with the
    /// tenant, or the tables of the tenant's schema or datasource. Null for one of the whole application, which hears
    /// every tenant's.
    /// </summary>
    public string? Tenant { get; set; }

    /// <summary>Gets headers sent with each message, such as an authorization the receiver expects.</summary>
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets a value indicating whether it is sent messages; a disabled one keeps its place.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets where it came from: code, configuration, or the webhooks kept by DapperMatic, whose addresses may
    /// not reach private networks.
    /// </summary>
    public string? Source { get; set; }
}

/// <summary>
/// How webhooks' messages are sent.
/// </summary>
public sealed class DapperMaticWebhookOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether this server sends messages: its background worker runs. Off on servers
    /// that only take requests, when others send; changes are noted either way.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets how often the changes noted are looked at.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Gets or sets how long an address has to answer.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets how many times a message is tried before it is dropped and the next one sent: the waits between
    /// grow from 2 seconds to an hour.
    /// </summary>
    public int MaxAttempts { get; set; } = 12;

    /// <summary>Gets or sets how long changes are kept in the datasource, sent or not.</summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets a value indicating whether webhooks kept through the API may post to loopback, private, link-local
    /// and other internal addresses. Off: an API caller must not reach the servers' own network. Webhooks given in code
    /// or configuration may always.
    /// </summary>
    public bool AllowPrivateAddresses { get; set; }
}

/// <summary>
/// A table or view whose queries callers may subscribe to (<c>GET …/subscribe</c>): its changes made through
/// DapperMatic are noted in the datasource, and each subscription's query is run again when a table it reads changes.
/// </summary>
public sealed class DapperMaticRealtimeTable
{
    /// <summary>Gets or sets the datasource, or null for the table in every datasource.</summary>
    public string? DatasourceId { get; set; }

    /// <summary>Gets or sets the table's schema, or null for the table in every schema.</summary>
    public string? SchemaName { get; set; }

    /// <summary>Gets or sets the table's or view's name.</summary>
    public required string TableName { get; set; }

    /// <summary>Gets or sets, for a view, the tables it reads (<c>table</c> or <c>schema.table</c>), whose changes run its subscriptions again.</summary>
    public string[] Reads { get; set; } = [];
}

/// <summary>
/// How subscriptions are kept current.
/// </summary>
public sealed class DapperMaticRealtimeOptions
{
    /// <summary>Gets or sets how often each server looks for changes other servers made.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets or sets how long changes are gathered before a subscription's query is run again.</summary>
    public TimeSpan Debounce { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Gets or sets how often an idle subscription is sent a comment, so that proxies keep it open.</summary>
    public TimeSpan Heartbeat { get; set; } = TimeSpan.FromSeconds(15);
}

/// <summary>
/// Where a webhook's messages have got to in a datasource.
/// </summary>
public sealed class DapperMaticWebhookStatus
{
    /// <summary>Gets or sets the last change taken or dropped.</summary>
    public long LastChange { get; set; }

    /// <summary>Gets or sets how many messages the address took.</summary>
    public long Delivered { get; set; }

    /// <summary>Gets or sets how many messages were dropped after every attempt failed.</summary>
    public long Dropped { get; set; }

    /// <summary>Gets or sets how many times the waiting message was tried.</summary>
    public int Attempts { get; set; }

    /// <summary>Gets or sets when it is tried next, UTC; null when none is waiting.</summary>
    public DateTime? NextAttemptAt { get; set; }

    /// <summary>Gets or sets why the last attempt failed.</summary>
    public string? LastError { get; set; }
}
