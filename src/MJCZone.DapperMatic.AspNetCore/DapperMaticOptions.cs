// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Caching.Distributed;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore;

/// <summary>
/// Options for configuring DapperMatic ASP.NET Core integration.
/// </summary>
/// <code>
/// // Illustrate appsettings.json configuration
/// {
///   "DapperMatic": {
///     "BasePath": "/api/dm",
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
    // DapperMatic's own tables, by their default names (a renamed access table is added by the builder that names it).
    private static readonly string[] DefaultPrivateTables =
    [
        "dm_users",
        "dm_memberships",
        "dm_invitations",
        "dm_user_sessions",
        "dm_user_roles",
        "dm_user_claims",
        "dm_user_tokens",
        "dm_account_settings",
        "dm_data_protection_keys",
        "dm_accounts_state",
        "dm_tickets",
        "dm_api_keys",
        "dm_access",
        "dm_datasources",
        "dm_changes",
        "dm_webhook_state",
        "dm_table_state",
    ];

    /// <summary>
    /// Gets or sets the base path for DapperMatic API endpoints.
    /// Default is "/api/dm".
    /// </summary>
    public string BasePath { get; set; } = "/api/dm";

    /// <summary>
    /// Gets or sets which sets of endpoints are mapped: <see cref="DapperMaticEndpoints.Default"/> unless set, every set
    /// but those an application turns on itself (<see cref="DapperMaticEndpoints.Mcp"/>).
    /// </summary>
    public DapperMaticEndpoints Endpoints { get; set; } = DapperMaticEndpoints.Default;

    /// <summary>
    /// Gets or sets a role granted every action: shorthand for a policy granting <see cref="DmAction.All"/> to it.
    /// </summary>
    public string? RequireRole { get; set; }

    /// <summary>
    /// Gets or sets a role granted reading rows and definitions: shorthand for a policy granting
    /// <see cref="DmAction.Read"/> and <see cref="DmAction.ReadSchema"/> to it.
    /// </summary>
    public string? ReadOnlyRole { get; set; }

    /// <summary>
    /// Gets the policies: who may do what, to which tables, rows and columns. Nothing is allowed unless a policy
    /// grants it. Bound from configuration (<c>DapperMatic:Policies</c>) or added in code with
    /// <see cref="DapperMaticConfigurationBuilder.Policy(DmPolicy)"/>; see <see cref="DmPolicy"/>.
    /// </summary>
    public List<DmPolicy> Policies { get; } = [];

    /// <summary>
    /// Gets or sets how often the policies and tenants kept by DapperMatic are read again, so that each instance of the
    /// application sees changes made through another. An instance sees its own at once. Default is 30 seconds.
    /// </summary>
    public TimeSpan AccessRefreshInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how long a caller's session lasts at most: a stream (a subscription) or socket open longer ends, and
    /// its client signs in again, so that a caller who has lost their access does not keep it by staying connected. A
    /// signed-in token's expiry (its <c>exp</c> claim) ends a session sooner. Null for no limit but the token's. Default
    /// is one hour.
    /// </summary>
    public TimeSpan? MaxSessionAge { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets how many callers' models are kept: what each caller may use of a datasource, which their OpenAPI
    /// document and GraphQL schema describe. Callers alike in everything the policies read (roles, the claims policies
    /// name, the tenant) share one. Kept as long as table descriptions (<see cref="SchemaCacheDuration"/>), and built
    /// again when the policies, tenants or schema change. 0 to keep none. Default is 1,000.
    /// </summary>
    public int CallerModelCacheSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets what tells callers apart when code decides what they may do (a policy's <c>Predicate</c>, or
    /// permissions of the application's own), which DapperMatic cannot read: a key for the caller of an operation, equal
    /// for callers who may use exactly the same, or null for one whose model is not to be kept. Without it, the models of
    /// such callers are built for every request.
    /// </summary>
    public Func<DapperMaticOperation, string?>? CallerModelCacheKey { get; set; }

    /// <summary>
    /// Gets the claim types copied into every audit event (<see cref="Auditing.DapperMaticAuditEvent.Claims"/>), such
    /// as the id of the grant a platform administrator acts in a tenant under, or an "acting as" claim.
    /// </summary>
    public List<string> AuditClaims { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether error responses carry the error's own message, which for a database
    /// error is the database's text: names of tables, columns and constraints, and sometimes values. Null, the
    /// default, includes it in the Development environment only.
    /// </summary>
    public bool? IncludeErrorDetails { get; set; }

    /// <summary>
    /// Gets the list of datasources to register during startup.
    /// </summary>
    public List<DatasourceDto> Datasources { get; } = [];

    /// <summary>
    /// Gets or sets the key used to encrypt connection strings in a file or database repository: a base64-encoded
    /// 256-bit key, such as <c>CryptoUtils.GenerateEncryptionKey()</c> returns. The in-memory repository needs none.
    /// </summary>
    public string? ConnectionStringEncryptionKey { get; set; }

    /// <summary>
    /// Gets the keys connection strings were encrypted with before the current one, so that a key can be rotated:
    /// connection strings they encrypted are still read, and are written again with the current key.
    /// </summary>
    public List<string> PreviousConnectionStringEncryptionKeys { get; } = [];

    /// <summary>
    /// Gets what a datasource added or changed through the API may connect to.
    /// </summary>
    public DapperMaticConnectionPolicy ConnectionPolicy { get; } = new();

    /// <summary>
    /// Gets or sets how long a table's description, read from the database, is reused. The description is
    /// forgotten at once when DapperMatic's own endpoints change the table. Zero turns the cache off.
    /// </summary>
    public TimeSpan SchemaCacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the tables no DapperMatic endpoint serves, by name, in any datasource and schema: they are not listed, read,
    /// written, altered, dropped or created through the API, GraphQL or MCP, nor followed as a relation or referenced by
    /// a foreign key, whatever the policies or the application's permissions say.
    /// </summary>
    /// <remarks>
    /// It starts with DapperMatic's own tables (accounts, keys, tickets, kept access, datasources, the change log, the
    /// migration ledgers), which may share a datasource the API serves. Add tables of your own the API must never
    /// reach. A view's SQL is not held to it: defining views reaches every table of a datasource, which is why only a
    /// grant on a whole datasource covers it.
    /// </remarks>
    public HashSet<string> PrivateTables { get; } = new(DefaultPrivateTables, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the relations query requests may follow from tables: none is reachable unless declared here.
    /// </summary>
    public List<DapperMaticRelation> Relations { get; } = [];

    /// <summary>
    /// Gets the tables <c>$search</c> searches, and their columns: none is searchable unless declared here.
    /// </summary>
    public List<DapperMaticSearchable> Searchable { get; } = [];

    /// <summary>
    /// Gets the functions callers may run, each as policies granting <see cref="DmAction.Execute"/> allow: none is
    /// callable unless declared here.
    /// </summary>
    public List<DapperMaticFunction> Functions { get; } = [];

    /// <summary>
    /// Gets the webhooks given in code or configuration; those kept through the API are added to them.
    /// </summary>
    public List<DapperMaticWebhook> Webhooks { get; } = [];

    /// <summary>
    /// Gets how webhooks' messages are sent.
    /// </summary>
    public DapperMaticWebhookOptions WebhookDelivery { get; } = new();

    /// <summary>
    /// Gets the tables and views callers may subscribe to: none unless listed here.
    /// </summary>
    public List<DapperMaticRealtimeTable> RealtimeTables { get; } = [];

    /// <summary>
    /// Gets how subscriptions are kept current.
    /// </summary>
    public DapperMaticRealtimeOptions Realtime { get; } = new();

    /// <summary>
    /// Gets or sets the schema DapperMatic's change tables (<c>dm_changes</c>, <c>dm_webhook_state</c>) are made in, in
    /// each datasource that webhooks or subscriptions hear; null for the default one.
    /// </summary>
    public string? ChangeTablesSchema { get; set; }

    /// <summary>
    /// Gets the tables and views whose query results are kept, each for its own time: none is cached unless listed here.
    /// </summary>
    public List<DapperMaticCachedTable> CachedTables { get; } = [];

    /// <summary>
    /// Gets or sets where query results are kept: null for the application's <see cref="IDistributedCache"/> (Redis,
    /// Valkey, ...) when it registers one, else memory in this process.
    /// </summary>
    public IDistributedCache? ResultCacheStore { get; set; }

    /// <summary>
    /// Gets or sets what the keys of kept results and table versions start with: null for the application's name and
    /// environment, so that instances of one application share them, and other applications or environments using the
    /// same store (one Redis for staging and production) never do. Give the same value to applications that serve the
    /// same databases and should share results.
    /// </summary>
    public string? ResultCacheKeyPrefix { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether successful <c>GET</c> responses carry an <c>ETag</c>, which a client sends
    /// back in <c>If-None-Match</c> to be answered <c>304 Not Modified</c> while the response is unchanged. On by default.
    /// </summary>
    public bool ETags { get; set; } = true;

    /// <summary>
    /// Gets the limits on what a query request may ask for.
    /// </summary>
    public DapperMaticQueryLimits QueryLimits { get; } = new();

    /// <summary>
    /// Gets how a request's tenant is found, and what is known of it. See <see cref="DapperMaticTenancyOptions"/>.
    /// </summary>
    public DapperMaticTenancyOptions Tenancy { get; } = new();

    /// <summary>
    /// Gets the values set on each connection's session for the database's own row-level security, such as
    /// <c>app.tenant_id</c> = <c>@tenant.id</c>. SQLite has no sessions and is skipped.
    /// </summary>
    public List<DapperMaticSessionValue> Session { get; } = [];

    /// <summary>
    /// Gets the sign-in methods (authentication schemes) DapperMatic's endpoints accept, tried in order after
    /// DapperMatic's own (API keys, built-in accounts) once they are turned on; the first that signs the caller in is
    /// the caller. Empty (the default) for the application's default method, and a caller its authentication already
    /// signed in. Set with <c>dm.SignInWith(...)</c>.
    /// </summary>
    public List<string> SignInSchemes { get; } = [];

    /// <summary>
    /// Gets where callers' roles are found besides ASP.NET Core's role claim: other claims, JSON in a claim, code. The
    /// roles of every source add up. Set with <c>dm.RolesFrom(...)</c>.
    /// </summary>
    public List<Authentication.DapperMaticRoleSource> RoleSources { get; } = [];

    /// <summary>
    /// Gets how the tickets that open browsers' streams work (<c>POST …/auth/ticket</c>).
    /// </summary>
    public DapperMaticTicketOptions Tickets { get; } = new();

    /// <summary>
    /// Gets how API keys work, once turned on with <c>dm.AddApiKeys()</c>.
    /// </summary>
    public Authentication.DapperMaticApiKeyOptions ApiKeys { get; } = new();

    /// <summary>
    /// Gets how the built-in accounts work, once turned on with <c>dm.AddUserAccounts()</c>.
    /// </summary>
    public Accounts.DapperMaticAccountOptions Accounts { get; } = new();

    /// <summary>
    /// Gets the service accounts given in code or configuration, besides those kept through the access endpoints.
    /// </summary>
    public List<DapperMaticServiceAccount> ServiceAccounts { get; } = [];

    /// <summary>
    /// Gets DapperMatic's own sign-in methods, in the order they are tried: added as features are turned on.
    /// </summary>
    internal List<string> OwnSignInSchemes { get; } = [];

    /// <summary>
    /// Gets the drivers the application registered, by provider.
    /// </summary>
    internal Dictionary<DbProviderType, DbProviderFactory> ProviderFactories { get; } = [];

    /// <summary>
    /// Gets the steps given in code that run on each connection once it is opened for a request.
    /// </summary>
    internal List<Func<IDbConnection, DmAccessContext, CancellationToken, Task>> SessionSteps { get; } = [];

    /// <summary>
    /// Gets the request values given in code, read as <c>@values.name</c>.
    /// </summary>
    internal Dictionary<string, DmValueProvider> RequestValues { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// The sets of endpoints DapperMatic maps.
/// </summary>
[Flags]
public enum DapperMaticEndpoints
{
    /// <summary>No endpoints.</summary>
    None = 0,

    /// <summary>Listing, adding, changing and removing datasources, and testing their connections.</summary>
    Datasources = 1,

    /// <summary>Each provider's data types.</summary>
    DataTypes = 2,

    /// <summary>Schemas, tables, columns, indexes, constraints and views: reading their definitions and changing them.</summary>
    Schemas = 4,

    /// <summary>Querying the rows of tables and views.</summary>
    Query = 8,

    /// <summary>Inserting, updating and deleting rows.</summary>
    Rows = 16,

    /// <summary>
    /// Reading and changing the policies and tenants kept by DapperMatic, and a caller's effective permissions.
    /// </summary>
    Access = 32,

    /// <summary>Running the functions the options declare.</summary>
    Functions = 64,

    /// <summary>Subscriptions to queries, as Server-Sent Events.</summary>
    Realtime = 128,

    /// <summary>
    /// The Model Context Protocol endpoint (<c>/mcp</c>): DapperMatic as tools for AI assistants. Not in
    /// <see cref="Default"/>: an application turns it on.
    /// </summary>
    Mcp = 256,

    /// <summary>
    /// Signing in: <c>{BasePath}/auth/…</c> (tickets for streams, one's own API keys, the built-in accounts' endpoints
    /// once they are on).
    /// </summary>
    Auth = 512,

    /// <summary>The sets served unless the options say otherwise: every set but <see cref="Mcp"/>.</summary>
    Default = Datasources | DataTypes | Schemas | Query | Rows | Access | Functions | Realtime | Auth,

    /// <summary>Every set.</summary>
    All = Default | Mcp,
}

/// <summary>
/// How the tickets that open browsers' streams work: a browser's <c>EventSource</c> and WebSocket cannot send an
/// <c>Authorization</c> header, so a signed-in page asks for a ticket and opens the stream with <c>?ticket=</c>.
/// </summary>
public sealed class DapperMaticTicketOptions
{
    /// <summary>Gets or sets how long a ticket is good for, once. Default is 30 seconds.</summary>
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromSeconds(30);
}
