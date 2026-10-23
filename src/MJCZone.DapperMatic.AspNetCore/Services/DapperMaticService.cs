// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Runtime.CompilerServices;
using System.Text;
using Dapper;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Auditing;
using MJCZone.DapperMatic.AspNetCore.Caching;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Validation;

using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Implementation of IDapperMaticService for ASP.NET Core applications.
/// </summary>
public partial class DapperMaticService : IDapperMaticService
{
    private static readonly DmAction[] RowActions = [DmAction.Read, DmAction.Create, DmAction.Update, DmAction.Delete];

    private readonly IDapperMaticDatasourceRepository _datasourceRepository;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IDapperMaticPermissions _permissions;
    private readonly IDapperMaticAuditLogger _auditLogger;
    private readonly DapperMaticOptions _options;
    private readonly DapperMaticSchemaCache _schemaCache;
    private readonly DapperMaticPolicySet _policies;
    private readonly DapperMaticResultCache? _resultCache;
    private readonly Changes.DapperMaticChangeLog? _changeLog;
    private readonly Changes.DapperMaticRealtimeHub? _hub;
    private readonly DapperMaticCallerModelCache? _callerModels;
    private string? _session = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticService"/> class.
    /// </summary>
    /// <param name="datasourceRepository">The datasource repository.</param>
    /// <param name="connectionFactory">The connection factory.</param>
    /// <param name="permissions">The permissions manager.</param>
    /// <param name="auditLogger">The audit logger.</param>
    public DapperMaticService(
        IDapperMaticDatasourceRepository datasourceRepository,
        IDbConnectionFactory connectionFactory,
        IDapperMaticPermissions permissions,
        IDapperMaticAuditLogger auditLogger
    )
        : this(datasourceRepository, connectionFactory, permissions, auditLogger, Options.Create(new DapperMaticOptions())) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticService"/> class, with the options that declare the
    /// relations query requests may follow and the limits on what they may ask for.
    /// </summary>
    /// <param name="datasourceRepository">The datasource repository.</param>
    /// <param name="connectionFactory">The connection factory.</param>
    /// <param name="permissions">The permissions manager.</param>
    /// <param name="auditLogger">The audit logger.</param>
    /// <param name="options">The options.</param>
    /// <param name="schemaCache">The cache of table and view descriptions, or null for one of the service's own.</param>
    /// <param name="policies">The policies in force, or null for those of the options.</param>
    /// <param name="resultCache">The query results kept for the tables the options list, or null to keep none.</param>
    /// <param name="changeLog">Where the changes webhooks and subscriptions hear are noted, or null to note none.</param>
    /// <param name="hub">The subscriptions open on this server, told of its changes at once; or null.</param>
    /// <param name="callerModels">The callers' models kept, or null to keep none.</param>
    public DapperMaticService(
        IDapperMaticDatasourceRepository datasourceRepository,
        IDbConnectionFactory connectionFactory,
        IDapperMaticPermissions permissions,
        IDapperMaticAuditLogger auditLogger,
        IOptions<DapperMaticOptions> options,
        DapperMaticSchemaCache? schemaCache = null,
        DapperMaticPolicySet? policies = null,
        DapperMaticResultCache? resultCache = null,
        Changes.DapperMaticChangeLog? changeLog = null,
        Changes.DapperMaticRealtimeHub? hub = null,
        DapperMaticCallerModelCache? callerModels = null
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        _datasourceRepository = datasourceRepository;
        _connectionFactory = connectionFactory;
        _permissions = permissions;
        _auditLogger = auditLogger;
        _options = options.Value;
        _schemaCache = schemaCache ?? new DapperMaticSchemaCache(_options.SchemaCacheDuration);
        _policies = policies ?? new DapperMaticPolicySet(options);
        _resultCache = resultCache;
        _changeLog = changeLog;
        _hub = hub;
        _callerModels = callerModels;
    }

    /// <summary>
    /// Keys the results the database's row-level security filtered by the session values it read: their text as the
    /// database reads it (a list as its items, never its type's name), each part's length first, so no two sets of
    /// values share a key.
    /// </summary>
    /// <param name="values">The session values, by name.</param>
    /// <returns>The key.</returns>
    internal static string SessionKey(IEnumerable<KeyValuePair<string, object?>> values) =>
        string.Join(
            '\u001f',
            values.Select(v => DmSession.Text(v.Value) is { } text ? $"{v.Key.Length}:{v.Key}={text.Length}:{text}" : $"{v.Key.Length}:{v.Key}=null")
        );

    /// <summary>
    /// Names the schema of an operation when neither its request nor its tenant names one: the datasource's default
    /// schema (dbo, public), where the operation acts. It is authorized as acting there, so that leaving the schema out
    /// never passes over a policy about schemas.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <returns>The operation, in the schema it acts in.</returns>
    internal async Task<DapperMaticOperation> ResolveSchemaAsync(DapperMaticOperation context)
    {
        if (context.DatasourceId is not { Length: > 0 } datasourceId
            || context.SchemaName is { Length: > 0 } and not "_"
            || context.Access?.Tenant?.SchemaName is not null
            || !DapperMaticPolicySet.ActsInSchema(context.Operation))
        {
            return context;
        }

        return context with { SchemaName = await DefaultSchemaOfAsync(datasourceId).ConfigureAwait(false) };
    }

    /// <summary>
    /// Opens a datasource's connection for typed code run as the request's caller, with the session values the
    /// database's own row-level security reads.
    /// </summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <returns>The connection.</returns>
    /// <param name="caller">The caller, whose values the session values read.</param>
    internal Task<System.Data.IDbConnection> OpenForCallerAsync(string datasourceId, DmAccessContext caller) => ConnectAsync(datasourceId, caller);

    /// <summary>
    /// Gets the schema a datasource puts a name with none in: dbo, public; null for providers without schemas, or a
    /// datasource that does not exist.
    /// </summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <returns>The schema name, or null.</returns>
    internal async Task<string?> DefaultSchemaOfAsync(string datasourceId)
    {
        var provider = (await _datasourceRepository.GetDatasourceAsync(datasourceId).ConfigureAwait(false))?.Provider;
        return provider is { Length: > 0 } && DapperMaticProviders.TryParse(provider, out var type) ? DapperMaticProviders.DefaultSchemaName(type) : null;
    }

    #region Shared Protected Methods

    /// <summary>
    /// Asserts that the operation context has the necessary permissions.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="methodName">The calling method name (automatically provided).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    protected virtual async Task AssertPermissionsAsync(
        DapperMaticOperation context,
        [CallerMemberName] string methodName = ""
    )
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        // Authorized as acting in its schema, and recorded as the service received it: a failure is audited so.
        var operation = (await ResolveSchemaAsync(context).ConfigureAwait(false)).WithProperty("CurrentMethod", methodName);
        context.State.Received ??= operation;

        // DapperMatic's own tables, and those the application keeps from the API, are served to no one.
        if (IsPrivate(operation.TableName))
        {
            context.State.Received = operation;
            throw new UnauthorizedAccessException($"DapperMatic does not serve the table '{operation.TableName}'.");
        }

        if (!await _permissions.IsAuthorizedAsync(operation).ConfigureAwait(false) || !WithinLimits(operation))
        {
            // A request refused partway (a later table of a batch or a mutation) is audited as what was refused.
            context.State.Received = operation;
            var msg = new StringBuilder();
            msg.Append($"Access denied for operation '{operation.Operation}'");
            if (!string.IsNullOrWhiteSpace(operation.DatasourceId))
            {
                msg.Append($" on datasource '{operation.DatasourceId}'");
                if (!string.IsNullOrWhiteSpace(operation.SchemaName))
                {
                    msg.Append($", '{operation.SchemaName}'");
                }
                if (!string.IsNullOrWhiteSpace(operation.TableName))
                {
                    msg.Append($", table '{operation.TableName}'");
                }
                if (!string.IsNullOrWhiteSpace(operation.ViewName))
                {
                    msg.Append($", view '{operation.ViewName}'");
                }
                if (operation.ColumnNames is { Count: > 0 })
                {
                    msg.Append($", columns ('{string.Join("', '", operation.ColumnNames)}')");
                }
            }
            msg.Append('.');
            throw new UnauthorizedAccessException(msg.ToString());
        }
    }

    /// <summary>
    /// Creates and opens a database connection for the specified datasource.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <returns>An open database connection.</returns>
    /// <exception cref="ArgumentException">Thrown when the datasource is not found.</exception>
    protected virtual Task<IDbConnection> CreateConnectionForDatasource(string datasourceId) =>
        ConnectAsync(datasourceId, DapperMaticCallerSession.Current?.Access);

    /// <summary>
    /// Logs an audit event for the specified operation context.
    /// </summary>
    /// <param name="operation">The operation context.</param>
    /// <param name="success">Indicates if the operation was successful.</param>
    /// <param name="errorMessage">Optional error message if the operation failed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected virtual async Task LogAuditEventAsync(
        DapperMaticOperation operation,
        bool success,
        string? errorMessage = null
    )
    {
        var auditEvent = (operation.State.Received ?? operation).ToAuditEvent(success, errorMessage, _options.AuditClaims);
        await _auditLogger.LogOperationAsync(auditEvent).ConfigureAwait(false);
    }

    #endregion // Shared Protected Methods

    #region Shared Private Methods

    // Opens a datasource's connection; with a caller, in a session with the values the database's own row-level
    // security reads.
    private async Task<IDbConnection> ConnectAsync(string datasourceId, DmAccessContext? caller)
    {
        var datasource =
            await _datasourceRepository.GetDatasourceAsync(datasourceId).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Datasource '{datasourceId}' not found.");

        if (string.IsNullOrWhiteSpace(datasource.Provider))
        {
            throw new InvalidOperationException($"Datasource '{datasourceId}' is missing a provider.");
        }

        var connectionString = await _datasourceRepository.GetConnectionStringAsync(datasourceId).ConfigureAwait(false);

        if (connectionString == null)
        {
            throw new InvalidOperationException($"Connection string for datasource '{datasourceId}' is not available.");
        }

        var connection = _connectionFactory.CreateConnection(datasource.Provider, connectionString);
        if (caller is { } access && (_options.Session.Count > 0 || _options.SessionSteps.Count > 0))
        {
            // The database's own row-level security reads the caller from the session: the connection stays open for
            // the request, so that every statement runs in the session the values were set on.
            access = access.ForDatasource(datasourceId);
            connection.Open();
            if (_options.Session.Count > 0 && connection.GetDbProviderType() != DbProviderType.Sqlite)
            {
                var values = new List<KeyValuePair<string, object?>>();
                foreach (var session in _options.Session)
                {
                    var value = session.Value is { Length: > 1 } path && path[0] == '@'
                        ? await access.GetValueAsync(path).ConfigureAwait(false)
                        : session.Value;
                    values.Add(new KeyValuePair<string, object?>(session.Name, value));
                }

                await DmSession.SetAsync(connection, values).ConfigureAwait(false);

                // Results the database's row-level security filtered are kept per session values, never shared.
                _session = SessionKey(values);
            }

            // A session step sets what it likes, which results cannot be keyed by: they are not kept.
            if (_options.SessionSteps.Count > 0)
            {
                _session = null;
            }

            foreach (var step in _options.SessionSteps)
            {
                await step(connection, access, CancellationToken.None).ConfigureAwait(false);
            }
        }

        return connection;
    }

    // An API key's limits hold whatever permissions decide: the application's own too. Rows are held to them table by
    // table (RulesFor); anything else here.
    private static bool WithinLimits(DapperMaticOperation operation)
    {
        var access = AccessOf(operation);
        var action = DapperMaticPolicySet.ActionOf(operation.Operation);
        return access.Limits is null
            || action is DmAction.Read or DmAction.Create or DmAction.Update or DmAction.Delete
            || DmPolicies.Limit(DmAccessRules.Unrestricted, access, DapperMaticPolicySet.ResourceOf(operation), action).Allowed;
    }

    // The request's access context; outside a request, one of the operation's caller.
    private static DmAccessContext AccessOf(DapperMaticOperation context) =>
        (context.Access ?? new DmAccessContext(context.User)).ForDatasource(context.DatasourceId);

    // What the policies allow on a resource. With permissions of the application's own, a resource no policy is about
    // is the application's to decide, and its permissions have decided; but a table whose rows a policy is about, for
    // any of reading, inserting, updating or deleting, is held for all four, as PostgreSQL holds a table with row
    // security on: an action no policy grants is refused there.
    private DmAccessRules RulesFor(DapperMaticOperation context, DmResource resource, DmAction action)
    {
        var access = AccessOf(context);
        var rules = _policies.Evaluate(access, resource, action);
        if (rules.Targeted || _permissions is DefaultDapperMaticPermissions)
        {
            return rules;
        }

        // Where code decides, an API key's limits still hold.
        return (action & DmAction.ReadWrite) != 0
            && RowActions.Any(a => a != action && _policies.Evaluate(access, resource, a).Targeted)
            ? rules
            : DmPolicies.Limit(DmAccessRules.Unrestricted, access, resource, action);
    }

    // The schema a resource is in: the one named, or the provider's default.
    private static string? SchemaOf(IDbConnection connection, string? schemaName) =>
        schemaName is { Length: > 0 } and not "_" ? schemaName : DapperMaticProviders.DefaultSchemaName(connection.GetDbProviderType());

    // Authorizes the request as another operation on another table or view: for what an operation reads or names besides
    // its own table, as a related table, a referenced one or a rename's new name.
    private Task AssertAsAsync(
        DapperMaticOperation context,
        string operation,
        string? schemaName,
        string? tableName,
        string? viewName,
        string methodName
    ) =>
        AssertPermissionsAsync(
            context with
            {
                Operation = operation,
                SchemaName = schemaName,
                TableName = tableName,
                ViewName = viewName,
                ColumnNames = null,
                State = new(),
            },
            methodName
        );

    // Whether the request may run as another operation on another table, refusing nothing: for what a request may leave
    // undone, as a related row's update. Checked as AssertPermissionsAsync checks.
    private async Task<bool> MayAsync(DapperMaticOperation context, string operation, string? schemaName, string tableName, string methodName)
    {
        if (IsPrivate(tableName))
        {
            return false;
        }

        var other = (
            await ResolveSchemaAsync(
                    context with
                    {
                        Operation = operation,
                        SchemaName = schemaName,
                        TableName = tableName,
                        ViewName = null,
                        ColumnNames = null,
                        State = new(),
                    }
                )
                .ConfigureAwait(false)
        ).WithProperty("CurrentMethod", methodName);
        return await _permissions.IsAuthorizedAsync(other).ConfigureAwait(false) && WithinLimits(other);
    }

    // A foreign key reads the table it references (an insert tells whether a key exists there) and keeps that table's
    // rows from being deleted: creating one needs the referenced table's definition, and its key columns, readable.
    private async Task AssertMayReferenceAsync(
        DapperMaticOperation context,
        IDbConnection connection,
        string datasourceId,
        string? schemaName,
        IEnumerable<(string? Table, IReadOnlyCollection<string> Columns)> references,
        string? self,
        CancellationToken cancellationToken
    )
    {
        foreach (var (referenced, keys) in references)
        {
            if (string.IsNullOrWhiteSpace(referenced) || string.Equals(referenced, self, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            await AssertAsAsync(context, "tables/get", schemaName, referenced, null, "ReferenceTable").ConfigureAwait(false);
            await AssertTableExistsAsync(datasourceId, referenced, schemaName, connection, cancellationToken).ConfigureAwait(false);
            var table = await DescribeTableAsync(datasourceId, connection, schemaName, referenced, cancellationToken).ConfigureAwait(false);
            var resource = new DmResource(datasourceId, SchemaOf(connection, table.SchemaName ?? schemaName), table.Name, [.. table.Columns.Select(c => c.Name)]);
            var rules = RulesFor(context, resource, DmAction.Read);
            var columns = keys.Count > 0 ? keys : table.Keys;
            if (!rules.Allowed || (rules.Columns is { } readable && columns.Any(c => !readable.Contains(c))))
            {
                throw new UnauthorizedAccessException($"A foreign key to {referenced} reads its {string.Join(", ", columns)}, which this caller may not read.");
            }
        }
    }

    // The tables a table's foreign keys reference, with the columns each reads.
    private static IEnumerable<(string? Table, IReadOnlyCollection<string> Columns)> References(MJCZone.DapperMatic.Models.DmTable table) =>
        [
            .. table.ForeignKeyConstraints.Select(f => ((string?)f.ReferencedTableName, (IReadOnlyCollection<string>)[.. f.ReferencedColumns.Select(c => c.ColumnName)])),
            .. table.Columns.Where(c => c.IsForeignKey).Select(c => (c.ReferencedTableName, ColumnsOf(c.ReferencedColumnName))),
        ];

    private static IReadOnlyCollection<string> ColumnsOf(string? column) => string.IsNullOrWhiteSpace(column) ? [] : [column];

    // A view's definition is SQL that reads whatever the connection can read: writing one needs DefineViews, granted
    // for the whole datasource to callers trusted to read everything in it.
    private void AssertMayDefineViews(DapperMaticOperation context, string datasourceId)
    {
        if (!RulesFor(context, new DmResource(datasourceId, DmResource.Any, null), DmAction.DefineViews).Allowed)
        {
            throw new UnauthorizedAccessException(
                "A view's SQL can read any table the connection can: writing one needs DmAction.DefineViews for the whole datasource."
            );
        }
    }

    // Whether a listing shows a datasource, schema, table or view.
    private bool MayReadSchema(DapperMaticOperation context, DmResource resource) => RulesFor(context, resource, DmAction.ReadSchema).Allowed;

    // A table no endpoint serves (DapperMaticOptions.PrivateTables).
    private bool IsPrivate(string? tableName) => tableName is { Length: > 0 } && _options.PrivateTables.Contains(tableName);

    private string? NormalizeSchemaName(string? schemaName)
    {
        if (string.IsNullOrWhiteSpace(schemaName) || schemaName == "_")
        {
            // None named: the tenant's schema, or the default one.
            return DapperMaticCallerSession.Current?.Tenant?.SchemaName;
        }
        return schemaName;
    }

    private static async Task AssertSchemaExistsIfSpecifiedAsync(
        string datasourceId,
        string? schemaName,
        IDbConnection connection,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            var schemaExists = await connection
                .DoesSchemaExistAsync(schemaName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!schemaExists)
            {
                throw new KeyNotFoundException($"Schema '{schemaName}' not found in datasource '{datasourceId}'");
            }
        }
    }

    private static async Task AssertSchemaDoesNotExistAsync(
        string datasourceId,
        string? schemaName,
        IDbConnection connection,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            var schemaExists = await connection
                .DoesSchemaExistAsync(schemaName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (schemaExists)
            {
                throw new DuplicateKeyException($"Schema '{schemaName}' already exists in datasource '{datasourceId}'");
            }
        }
    }

    private static async Task AssertTableExistsAsync(
        string datasourceId,
        string tableName,
        string? schemaName,
        IDbConnection connection,
        CancellationToken cancellationToken
    )
    {
        if (
            !await connection
                .DoesTableExistAsync(schemaName, tableName, cancellationToken: cancellationToken)
                .ConfigureAwait(false)
        )
        {
            throw new KeyNotFoundException(
                !string.IsNullOrWhiteSpace(schemaName)
                    ? $"Table '{tableName}' not found in schema '{schemaName}' of datasource '{datasourceId}'"
                    : $"Table '{tableName}' not found in datasource '{datasourceId}'"
            );
        }
    }

    private static async Task AssertTableDoesNotExistAsync(
        string datasourceId,
        string tableName,
        string? schemaName,
        IDbConnection connection,
        CancellationToken cancellationToken
    )
    {
        if (
            await connection
                .DoesTableExistAsync(schemaName, tableName, cancellationToken: cancellationToken)
                .ConfigureAwait(false)
        )
        {
            throw new DuplicateKeyException(
                !string.IsNullOrWhiteSpace(schemaName)
                    ? $"Table '{tableName}' already exists in schema '{schemaName}' of datasource '{datasourceId}'"
                    : $"Table '{tableName}' already exists in datasource '{datasourceId}'"
            );
        }
    }

    private static async Task AssertViewExistsAsync(
        string datasourceId,
        string viewName,
        string? schemaName,
        IDbConnection connection,
        CancellationToken cancellationToken
    )
    {
        if (
            !await connection
                .DoesViewExistAsync(schemaName, viewName, cancellationToken: cancellationToken)
                .ConfigureAwait(false)
        )
        {
            throw new KeyNotFoundException(
                !string.IsNullOrWhiteSpace(schemaName)
                    ? $"View '{viewName}' not found in schema '{schemaName}' of datasource '{datasourceId}'"
                    : $"View '{viewName}' not found in datasource '{datasourceId}'"
            );
        }
    }

    private static async Task AssertViewDoesNotExistAsync(
        string datasourceId,
        string viewName,
        string? schemaName,
        IDbConnection connection,
        CancellationToken cancellationToken
    )
    {
        if (
            await connection
                .DoesViewExistAsync(schemaName, viewName, cancellationToken: cancellationToken)
                .ConfigureAwait(false)
        )
        {
            throw new DuplicateKeyException(
                !string.IsNullOrWhiteSpace(schemaName)
                    ? $"View '{viewName}' already exists in schema '{schemaName}' of datasource '{datasourceId}'"
                    : $"View '{viewName}' already exists in datasource '{datasourceId}'"
            );
        }
    }

    #endregion // Shared Private Methods
}
